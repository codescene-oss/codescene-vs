using Codescene.VSExtension.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.Core.Util
{
    /// <summary>
    /// Tracks currently running delta analysis jobs and notifies subscribers when jobs start or finish.
    /// </summary>
    public static class DeltaJobTracker
    {
        private static readonly HashSet<Job> _running = new HashSet<Job>();
        public static event Action<Job> JobStarted;
        public static event Action<Job> JobFinished;

        public static void Add(Job job)
        {
            lock (_running)
            {
                if (_running.Add(job))
                    JobStarted?.Invoke(job);
            }
        }

        public static void Remove(Job job)
        {
            lock (_running)
            {
                if (_running.Remove(job))
                    JobFinished?.Invoke(job);
            }
        }

        /// <summary>
        /// Gets a thread-safe snapshot of all currently running delta jobs.
        /// </summary>
        public static IReadOnlyCollection<Job> RunningJobs
        {
            get
            {
                lock (_running) return _running.ToList().AsReadOnly();
            }
        }
    }
}
