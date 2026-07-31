// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    public partial class GitChangeObserverCore
    {
        private async Task ProcessDetectedFileQueueAsync(string filePath, CancellationToken token)
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
                    await ProcessFilesAsync(new[] { currentRequest }, token);
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

        private string GetBaselineCommit()
        {
            if (string.IsNullOrEmpty(_gitRootPath))
            {
                return null;
            }

            try
            {
                var repoPath = Repository.Discover(_gitRootPath);
                if (string.IsNullOrEmpty(repoPath))
                {
                    return null;
                }

                using (var repo = new Repository(repoPath))
                {
                    var mergeBaseFinder = new MergeBaseFinder(_logger);
                    var mergeBase = mergeBaseFinder.GetMergeBaseCommit(repo);
                    return mergeBase?.Sha;
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug($"GitChangeObserver: Could not get baseline commit: {ex.Message}");
                return null;
            }
        }
    }
}
