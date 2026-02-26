// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Services
{
    public class CodeHealthMonitorNotifier : ICodeHealthMonitorNotifier
    {
        private readonly ConcurrentDictionary<string, Job> _activeJobs = new ConcurrentDictionary<string, Job>();

        public event EventHandler ViewUpdateRequested;

        public void OnDeltaStarting(string filePath)
        {
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

        public void OnDeltaCompleted(string filePath)
        {
            if (_activeJobs.TryRemove(filePath, out var job))
            {
                DeltaJobTracker.Remove(job);
                ViewUpdateRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
