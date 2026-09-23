// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;

namespace Codescene.VSExtension.Core.Application.Cli
{
    internal static class ReviewStatusBarLogger
    {
        public static async Task<T> RunAsync<T>(ILogger logger, string path, ReviewAnnouncement announcement, CancellationToken cancellationToken, Func<Task<T>> work)
        {
            try
            {
                var result = await work().ConfigureAwait(false);
                LogFinished(logger, path, announcement, succeeded: true, cancellationToken);
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                LogFinished(logger, path, announcement, succeeded: false, cancellationToken);
                throw;
            }
        }

        public static bool HasReviewableContent(string path, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(Path.GetFileName(path));
        }

        private static void LogFinished(ILogger logger, string path, ReviewAnnouncement announcement, bool succeeded, CancellationToken cancellationToken)
        {
            if (announcement == null)
            {
                return;
            }

            if (!announcement.Announced)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var fileName = Path.GetFileName(path);
            if (succeeded)
            {
                logger?.Info($"Review complete for {fileName}.", true);
                return;
            }

            logger?.Info($"Review failed for {fileName}.", true);
        }
    }
}
