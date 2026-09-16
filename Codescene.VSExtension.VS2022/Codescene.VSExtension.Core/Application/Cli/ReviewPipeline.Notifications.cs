// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Cli
{
    public sealed partial class ReviewPipeline
    {
        private void OnReviewReceived(object sender, ReviewNotification notification)
        {
            if (notification == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(notification.Id))
            {
                _ = PresentWatchReviewAsync(notification);
                return;
            }

            lock (_gate)
            {
                if (!_pendingById.TryGetValue(notification.Id, out var pending) || pending.ReviewDone)
                {
                    return;
                }

                pending.ReviewDone = true;
                _presentation.ReviewFinished(pending.Document);
                if (!IsCurrent(pending, notification.RepoRoot, notification.Path) ||
                    !MatchesHash(notification.Result?.GitBlobSha, pending.ContentHash))
                {
                    _logger?.Warn($"[pipeline] ignoring fileReview id={notification.Id} path={notification.Path} repo={notification.RepoRoot}");
                    IgnorePending(pending);
                    return;
                }

                pending.ReviewResult = notification.Result;
                _presentation.PresentReview(ToPresentedReview(pending, notification.Result));
                TryComplete(pending);
            }
        }

        private void OnDeltaReceived(object sender, DeltaNotification notification)
        {
            if (notification == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(notification.Id))
            {
                _ = PresentWatchDeltaAsync(notification);
                return;
            }

            lock (_gate)
            {
                if (!_pendingById.TryGetValue(notification.Id, out var pending) || pending.DeltaDone)
                {
                    return;
                }

                pending.DeltaDone = true;
                _presentation.DeltaFinished(pending.Document);
                var hash = notification.Result?.NewGitBlobSha;
                if (IsCurrent(pending, notification.RepoRoot, notification.Path) && MatchesHash(hash, pending.ContentHash))
                {
                    pending.DeltaResult = notification.Result;
                    _presentation.PresentDelta(ToPresentedDelta(pending, notification.Result));
                }
                else
                {
                    _logger?.Warn($"[pipeline] ignoring deltaReview id={notification.Id} path={notification.Path} repo={notification.RepoRoot}");
                }

                TryComplete(pending);
            }
        }

        private void OnReviewFailed(object sender, ReviewFailedNotification failure)
        {
            if (failure == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(failure.Id))
            {
                _logger?.Warn($"[pipeline] watch reviewFailed path={failure.Path} repo={failure.RepoRoot}: {failure.Message}");
                return;
            }

            lock (_gate)
            {
                if (!_pendingById.TryGetValue(failure.Id, out var pending))
                {
                    return;
                }

                if (!IsCurrent(pending, failure.RepoRoot, failure.Path))
                {
                    IgnorePending(pending);
                    return;
                }

                var error = new Exception(failure.Message);
                CompletePending(pending, error);
                _latestByPath.Remove(pending.PathKey);
                _presentation.Failed(error);
            }
        }

        private void OnServerError(object sender, Exception error)
        {
            FailAll(error ?? new Exception("CodeScene IDE server error"));
        }

        private async Task PresentWatchReviewAsync(ReviewNotification notification)
        {
            var presented = await WatchPresentationAsync(notification.RepoRoot, notification.Path, notification.Result?.GitBlobSha).ConfigureAwait(false);
            if (presented == null)
            {
                return;
            }

            lock (_gate)
            {
                CacheWatchReview(PathKey(notification.RepoRoot, presented.RelPath), presented.Content, notification.Result);
            }

            _presentation.PresentReview(new PresentedReview
            {
                Document = presented.Document,
                RelPath = presented.RelPath,
                Content = presented.Content,
                UpdateDiagnosticsPane = presented.UpdateDiagnosticsPane,
                UpdateMonitor = presented.UpdateMonitor,
                Result = notification.Result,
            });
        }

        private async Task PresentWatchDeltaAsync(DeltaNotification notification)
        {
            var presented = await WatchPresentationAsync(notification.RepoRoot, notification.Path, notification.Result?.NewGitBlobSha).ConfigureAwait(false);
            if (presented == null)
            {
                return;
            }

            _presentation.PresentDelta(new PresentedDelta
            {
                Document = presented.Document,
                RelPath = presented.RelPath,
                Content = presented.Content,
                UpdateDiagnosticsPane = presented.UpdateDiagnosticsPane,
                UpdateMonitor = presented.UpdateMonitor,
                Result = notification.Result,
            });
        }

        private void CacheWatchReview(string pathKey, string content, CliReviewModel result)
        {
            if (content == null)
            {
                return;
            }

            RemoveWatchReview(pathKey);
            _watchReviews[pathKey] = new WatchReviewEntry
            {
                ContentHash = GitBlobSha.FromUtf8(content),
                DedupEpoch = _dedupEpoch,
                Result = result,
            };
            _watchReviewOrder.Add(pathKey);
            if (_watchReviews.Count > MaxWatchReviews)
            {
                RemoveWatchReview(_watchReviewOrder[0]);
            }
        }

        private void RemoveWatchReview(string pathKey)
        {
            if (_watchReviews.Remove(pathKey))
            {
                _watchReviewOrder.Remove(pathKey);
            }
        }

        private async Task<ReviewSubmission> WatchPresentationAsync(string repoRoot, string relPath, string receivedSha)
        {
            var posixPath = RpcPath.ToPosixRelPath(relPath);
            var filePath = RpcPath.CombineRepo(repoRoot, posixPath);
            var document = _fileAccess.FindOpenDocument(filePath);
            if (document == null)
            {
                try
                {
                    document = await _fileAccess.OpenDocumentAsync(filePath).ConfigureAwait(false);
                }
                catch
                {
                    document = null;
                }
            }

            if (document == null)
            {
                return null;
            }

            var expectedSha = await CurrentShaAsync(document, filePath).ConfigureAwait(false);
            if (receivedSha != null && receivedSha != expectedSha)
            {
                _logger?.Warn($"[pipeline] ignoring watch result path={posixPath} repo={repoRoot} stale sha");
                return null;
            }

            return new ReviewSubmission
            {
                Document = document,
                RelPath = posixPath,
                Content = document.Content,
                UpdateDiagnosticsPane = _fileAccess.IsVisible(filePath),
                UpdateMonitor = true,
            };
        }

        private async Task<string> CurrentShaAsync(ReviewDocument document, string filePath)
        {
            if (document.IsDirty)
            {
                return GitBlobSha.FromUtf8(document.Content);
            }

            var bytes = await _fileAccess.ReadFileBytesAsync(filePath).ConfigureAwait(false);
            return bytes != null ? GitBlobSha.FromBytes(bytes) : GitBlobSha.FromUtf8(document.Content);
        }

        private void FailAll(Exception error)
        {
            List<PendingReview> pendingReviews;
            lock (_gate)
            {
                pendingReviews = _pendingById.Values.ToList();
                foreach (var pending in pendingReviews)
                {
                    CompletePending(pending, error);
                }

                _latestByPath.Clear();
            }

            if (pendingReviews.Count > 0)
            {
                _presentation.Failed(error);
            }
        }

        private void TryComplete(PendingReview pending)
        {
            if (pending.ReviewDone && pending.DeltaDone)
            {
                pending.Completion.TrySetResult((pending.ReviewResult, pending.DeltaResult));
                _pendingById.Remove(pending.Id);
            }
        }

        private void CompletePending(PendingReview pending, Exception error = null)
        {
            if (!pending.ReviewDone)
            {
                pending.ReviewDone = true;
                _presentation.ReviewFinished(pending.Document);
            }

            if (!pending.DeltaDone)
            {
                pending.DeltaDone = true;
                _presentation.DeltaFinished(pending.Document);
            }

            if (error != null)
            {
                pending.Completion.TrySetException(error);
            }
            else
            {
                pending.Completion.TrySetResult((pending.ReviewResult, pending.DeltaResult));
            }

            _pendingById.Remove(pending.Id);
        }

        private void IgnorePending(PendingReview pending)
        {
            CompletePending(pending);
        }

        private bool IsCurrent(PendingReview pending, string repoRoot, string relPath)
        {
            return RpcPath.PathsEqual(pending.RepoRoot, repoRoot)
                && RpcPath.ToPosixRelPath(pending.RelPath) == RpcPath.ToPosixRelPath(relPath)
                && _latestByPath.TryGetValue(pending.PathKey, out var latest)
                && ReferenceEquals(latest, pending)
                && GitBlobSha.FromUtf8(pending.Document.Content) == pending.ContentHash
                && (!_tombstones.TryGetValue(pending.PathKey, out var tombstone) || tombstone < pending.Generation);
        }

        private sealed class PendingReview
        {
            public ReviewDocument Document { get; set; }

            public string RelPath { get; set; }

            public string Content { get; set; }

            public bool UpdateDiagnosticsPane { get; set; }

            public bool UpdateMonitor { get; set; }

            public string Id { get; set; }

            public string RepoRoot { get; set; }

            public string DedupKey { get; set; }

            public string PathKey { get; set; }

            public string ContentHash { get; set; }

            public int Generation { get; set; }

            public bool ReviewDone { get; set; }

            public bool DeltaDone { get; set; }

            public bool Submitted { get; set; }

            public CliReviewModel ReviewResult { get; set; }

            public DeltaResponseModel DeltaResult { get; set; }

            public TaskCompletionSource<(CliReviewModel, DeltaResponseModel)> Completion { get; set; }
        }

        private sealed class SubmissionContext
        {
            public string RepoRoot { get; set; }

            public string PathKey { get; set; }

            public string ContentHash { get; set; }

            public string DedupKey { get; set; }
        }

        private sealed class WatchReviewEntry
        {
            public string ContentHash { get; set; }

            public int DedupEpoch { get; set; }

            public CliReviewModel Result { get; set; }
        }

        private sealed class NoOpPresentation : IReviewPipelinePresentation
        {
            public static readonly NoOpPresentation Instance = new NoOpPresentation();

            public void ReviewStarted(ReviewDocument document)
            {
            }

            public void ReviewFinished(ReviewDocument document)
            {
            }

            public void DeltaStarted(ReviewDocument document)
            {
            }

            public void DeltaFinished(ReviewDocument document)
            {
            }

            public void PresentReview(PresentedReview review)
            {
            }

            public void PresentDelta(PresentedDelta delta)
            {
            }

            public void Remove(ReviewDocument document)
            {
            }

            public void Failed(Exception error)
            {
            }
        }

        private sealed class DiskFileAccess : IReviewPipelineFileAccess
        {
            public static readonly DiskFileAccess Instance = new DiskFileAccess();

            public ReviewDocument FindOpenDocument(string filePath)
            {
                return null;
            }

            public Task<ReviewDocument> OpenDocumentAsync(string filePath)
            {
                try
                {
                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    {
                        return Task.FromResult<ReviewDocument>(null);
                    }

                    return Task.FromResult(new ReviewDocument
                    {
                        FilePath = filePath,
                        Content = File.ReadAllText(filePath),
                        IsDirty = false,
                    });
                }
                catch
                {
                    return Task.FromResult<ReviewDocument>(null);
                }
            }

            public Task<byte[]> ReadFileBytesAsync(string filePath)
            {
                try
                {
                    return Task.FromResult(File.Exists(filePath) ? File.ReadAllBytes(filePath) : null);
                }
                catch
                {
                    return Task.FromResult<byte[]>(null);
                }
            }

            public bool IsVisible(string filePath)
            {
                return false;
            }
        }
    }
}
