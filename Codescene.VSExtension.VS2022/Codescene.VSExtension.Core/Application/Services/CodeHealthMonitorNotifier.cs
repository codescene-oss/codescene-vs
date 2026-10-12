// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Services
{
    [Export(typeof(ICodeHealthMonitorNotifier))]
    [Export(typeof(CodeHealthMonitorNotifier))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CodeHealthMonitorNotifier : ICodeHealthMonitorNotifier
    {
        private readonly ConcurrentDictionary<string, Job> _activeJobs = new ConcurrentDictionary<string, Job>();
        private readonly object _lock = new object();
        private bool _automaticAnalysisEnabled = true;

        public event EventHandler ViewUpdateRequested;

        public string AnalysisState { get; private set; } = "idle";

        public int? TotalCount { get; private set; }

        public int RemainingCount { get; private set; }

        public void SetAutomaticAnalysisEnabled(bool enabled)
        {
            lock (_lock)
            {
                if (_automaticAnalysisEnabled == enabled)
                {
                    return;
                }

                _automaticAnalysisEnabled = enabled;
                if (!enabled)
                {
                    SetStoppedUnlocked();
                    return;
                }

                if (AnalysisState == "stopped")
                {
                    AnalysisState = "idle";
                    ViewUpdateRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void OnDeltaStarting(string filePath)
        {
            lock (_lock)
            {
                if (!_automaticAnalysisEnabled)
                {
                    return;
                }

                if (_activeJobs.TryGetValue(filePath, out var existingJob))
                {
                    DeltaJobTracker.Remove(existingJob);
                }

                var job = new Job
                {
                    Type = WebComponentConstants.JobTypes.DELTA,
                    State = WebComponentConstants.StateTypes.RUNNING,
                    File = new File { FileName = filePath },
                };
                _activeJobs[filePath] = job;
                DeltaJobTracker.Add(job);
                ViewUpdateRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public void OnDeltaCompleted(string filePath)
        {
            lock (_lock)
            {
                if (_activeJobs.TryRemove(filePath, out var job))
                {
                    DeltaJobTracker.Remove(job);
                    ViewUpdateRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void ApplyQueue(ReviewQueue queue)
        {
            lock (_lock)
            {
                if (!_automaticAnalysisEnabled)
                {
                    SetStoppedUnlocked();
                    return;
                }

                _activeJobs.Clear();
                var files = queue?.Files ?? Array.Empty<string>();
                var jobs = new List<Job>();
                for (var i = 0; i < files.Count; i++)
                {
                    var filePath = files[i];
                    var job = new Job
                    {
                        Type = WebComponentConstants.JobTypes.DELTA,
                        State = i == 0 ? WebComponentConstants.StateTypes.RUNNING : WebComponentConstants.StateTypes.QUEUED,
                        File = new File { FileName = filePath },
                    };
                    _activeJobs[filePath] = job;
                    jobs.Add(job);
                }

                DeltaJobTracker.Replace(jobs);
                var remaining = queue?.Count ?? 0;
                if (remaining <= 0)
                {
                    RemainingCount = 0;
                    TotalCount = null;
                    AnalysisState = "idle";
                }
                else
                {
                    RemainingCount = remaining;
                    TotalCount = NextTotalCount(queue?.Done ?? 0, remaining);
                    AnalysisState = "running";
                }

                ViewUpdateRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _activeJobs.Clear();
                DeltaJobTracker.Clear();
                if (_automaticAnalysisEnabled)
                {
                    AnalysisState = "idle";
                    TotalCount = null;
                    RemainingCount = 0;
                }

                ViewUpdateRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RequestViewUpdate()
        {
            ViewUpdateRequested?.Invoke(this, EventArgs.Empty);
        }

        private int NextTotalCount(int done, int remaining)
        {
            var total = done + remaining;
            if (!TotalCount.HasValue)
            {
                return total;
            }

            return Math.Min(total, Math.Max(TotalCount.Value, remaining));
        }

        private void SetStoppedUnlocked()
        {
            _activeJobs.Clear();
            DeltaJobTracker.Clear();
            AnalysisState = "stopped";
            TotalCount = null;
            RemainingCount = 0;
            ViewUpdateRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
