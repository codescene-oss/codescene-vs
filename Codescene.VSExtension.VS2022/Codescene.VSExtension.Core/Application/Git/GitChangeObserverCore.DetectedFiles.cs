// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Git
{
    public partial class GitChangeObserverCore
    {
        private void RemoveStaleFilesFromCache(HashSet<string> changedFiles)
        {
            try
            {
                var filesToKeep = BuildFilesToKeep(changedFiles);
                var removedCount = new DeltaCacheService().RemoveEntriesNotIn(filesToKeep);

                if (removedCount > 0)
                {
                    _logger?.Debug($"Removed {removedCount} stale files from delta cache");
                    ViewUpdateRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn($"Error removing stale files: {ex.Message}");
            }
        }

        private HashSet<string> BuildFilesToKeep(HashSet<string> changedFiles)
        {
            var filesToKeep = new HashSet<string>(changedFiles, StringComparer.OrdinalIgnoreCase);

            AddVisibleFiles(filesToKeep);
            AddRunningJobFiles(filesToKeep);

            return filesToKeep;
        }

        private void AddVisibleFiles(HashSet<string> filesToKeep)
        {
            var visibleFiles = _openFilesObserver?.GetAllVisibleFileNames();
            if (visibleFiles == null)
            {
                return;
            }

            foreach (var f in visibleFiles)
            {
                filesToKeep.Add(f);
            }
        }

        private void AddRunningJobFiles(HashSet<string> filesToKeep)
        {
            foreach (var job in DeltaJobTracker.RunningJobs)
            {
                var fileName = job.File?.FileName;
                if (!string.IsNullOrEmpty(fileName))
                {
                    filesToKeep.Add(fileName);
                }
            }
        }

        private async Task ProcessDetectedFileQueueAsync(string filePath, CancellationToken token, string baselineCommit)
        {
            var currentRequest = filePath;
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                try
                {
#if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> GitChangeObserverCore: GitChangeLister detected 1 file");
#endif
                    await ProcessFilesAsync(new[] { currentRequest }, token, baselineCommit);
#if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> GitChangeObserverCore: Processed detected files");
#endif
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.Error("GitChangeObserver: Error processing detected files", ex);
                }

                if (!_detectedFilesQueue.CompleteAndGetNext(filePath, out currentRequest))
                {
                    return;
                }
            }
        }
    }
}
