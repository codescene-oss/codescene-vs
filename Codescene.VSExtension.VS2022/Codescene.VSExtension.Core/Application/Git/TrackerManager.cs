// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using Codescene.VSExtension.Core.Interfaces;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class TrackerManager
    {
        private readonly HashSet<string> _tracker = new HashSet<string>();
        private readonly object _lock = new object();
        private readonly ILogger _logger;

        public TrackerManager(ILogger logger = null)
        {
            _logger = logger;
        }

        public void Add(string filePath)
        {
            lock (_lock)
            {
                _tracker.Add(filePath);
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> TrackerManager: Added file to tracker: {filePath} (total: {_tracker.Count})");
                #endif
            }
        }

        public bool Contains(string filePath)
        {
            lock (_lock)
            {
                return _tracker.Contains(filePath);
            }
        }

        public bool Remove(string filePath)
        {
            lock (_lock)
            {
                var removed = _tracker.Remove(filePath);
                if (removed)
                {
                    #if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> TrackerManager: Removed file from tracker: {filePath} (total: {_tracker.Count})");
                    #endif
                }

                return removed;
            }
        }

        public List<string> GetFilesStartingWith(string prefix)
        {
            lock (_lock)
            {
                var matches = _tracker.Where(tf => tf.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase)).ToList();
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> TrackerManager: GetFilesStartingWith prefix '{prefix}' found {matches.Count} matches");
                #endif
                return matches;
            }
        }

        public void RemoveAll(List<string> filesToRemove)
        {
            lock (_lock)
            {
                #if FEATURE_INITIAL_GIT_OBSERVER
                _logger?.Info($">>> TrackerManager: Removing {filesToRemove.Count} files from tracker");
                #endif
                foreach (var file in filesToRemove)
                {
                    _tracker.Remove(file);
                }
            }
        }
    }
}
