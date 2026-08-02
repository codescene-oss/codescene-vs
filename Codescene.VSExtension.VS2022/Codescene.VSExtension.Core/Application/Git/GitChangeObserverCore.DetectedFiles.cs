// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;

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
    }
}
