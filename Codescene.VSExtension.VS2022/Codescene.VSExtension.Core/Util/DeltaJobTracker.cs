// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.Core.Util
{
    /// <summary>
    /// Tracks currently running delta analysis jobs and notifies subscribers when jobs start or finish.
    /// </summary>
    public static class DeltaJobTracker
    {
        private static readonly HashSet<Job> Running = new HashSet<Job>();

        public static event Action<Job> JobStarted;

        public static event Action<Job> JobFinished;

        /// <summary>
        /// Gets a thread-safe snapshot of all currently running delta jobs.
        /// </summary>
        public static IReadOnlyCollection<Job> RunningJobs
        {
            get
            {
                lock (Running)
                {
                    return Running.ToList().AsReadOnly();
                }
            }
        }

        public static void Add(Job job)
        {
            lock (Running)
            {
                if (Running.Add(job))
                {
                    JobStarted?.Invoke(job);
                }
            }
        }

        public static void Remove(Job job)
        {
            lock (Running)
            {
                if (Running.Remove(job))
                {
                    JobFinished?.Invoke(job);
                }
            }
        }
    }
}
