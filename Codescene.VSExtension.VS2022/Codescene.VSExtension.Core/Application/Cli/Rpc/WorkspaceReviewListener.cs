// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cache.Delta;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Rpc;

namespace Codescene.VSExtension.Core.Application.Cli.Rpc
{
    [Export(typeof(IWorkspaceReviewListener))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204", Justification = "Private static helpers follow the instance methods that use them.")]
    public sealed class WorkspaceReviewListener : IWorkspaceReviewListener
    {
        private readonly ILogger _logger;
        private readonly IModelMapper _mapper;
        private readonly ICodeHealthMonitorNotifier _notifier;
        private readonly IOpenDocumentContentProvider _openDocumentContentProvider;
        private readonly ConcurrentDictionary<string, PendingReview> _pendingById = new ConcurrentDictionary<string, PendingReview>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, string> _latestIdByPath = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _clientGate = new object();
        private IIdeServerClient _client;

        [ImportingConstructor]
        public WorkspaceReviewListener(
            ILogger logger,
            IModelMapper mapper,
            [Import(AllowDefault = true)] ICodeHealthMonitorNotifier notifier = null,
            [Import(AllowDefault = true)] IOpenDocumentContentProvider openDocumentContentProvider = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _notifier = notifier;
            _openDocumentContentProvider = openDocumentContentProvider;
        }

        public event EventHandler<FileReviewAppliedEventArgs> ReviewApplied;

        public event EventHandler<DeltaReviewAppliedEventArgs> DeltaApplied;

        public event EventHandler<ReviewFailedEventArgs> ReviewFailed;

        public void Attach(IIdeServerClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            lock (_clientGate)
            {
                DetachLocked();
                _client = client;
                _client.FileReviewReceived += OnFileReview;
                _client.DeltaReviewReceived += OnDeltaReview;
                _client.ReviewFailedReceived += OnReviewFailed;
            }
        }

        public void Detach()
        {
            lock (_clientGate)
            {
                DetachLocked();
            }
        }

        public string SubmitBufferReview(string repoRoot, string relPath, string absolutePath, string content, string baselineRevision = null)
        {
            var client = RequireClient();
            var id = Guid.NewGuid().ToString("D");
            var pending = new PendingReview
            {
                Id = id,
                RepoRoot = repoRoot,
                RelPath = relPath,
                AbsolutePath = absolutePath,
                Content = content,
                ContentHash = GitBlobSha.FromUtf8(content),
            };
            _pendingById[id] = pending;
            _latestIdByPath[PathKey(repoRoot, relPath)] = id;
            _notifier?.OnDeltaStarting(absolutePath);
            client.ReviewFiles(new ReviewFilesParams
            {
                RepoRoot = repoRoot,
                BaselineRevision = baselineRevision,
                Files = new List<ReviewFileItem>
                {
                    new ReviewFileItem { Id = id, RelPath = relPath, Content = content },
                },
            });
            return id;
        }

        public void SubmitDiskReviews(string repoRoot, IReadOnlyList<string> relPaths, string baselineRevision = null)
        {
            var client = RequireClient();
            if (relPaths == null || relPaths.Count == 0)
            {
                return;
            }

            var files = new List<ReviewFileItem>(relPaths.Count);
            foreach (var relPath in relPaths)
            {
                files.Add(new ReviewFileItem { RelPath = relPath });
            }

            client.ReviewFiles(new ReviewFilesParams
            {
                RepoRoot = repoRoot,
                BaselineRevision = baselineRevision,
                Files = files,
            });
        }

        private void DetachLocked()
        {
            if (_client == null)
            {
                return;
            }

            _client.FileReviewReceived -= OnFileReview;
            _client.DeltaReviewReceived -= OnDeltaReview;
            _client.ReviewFailedReceived -= OnReviewFailed;
            _client = null;
        }

        private IIdeServerClient RequireClient()
        {
            var client = _client;
            if (client == null)
            {
                throw new InvalidOperationException("Workspace review listener is not attached to an IDE server client.");
            }

            return client;
        }

        private void OnFileReview(object sender, FileReviewNotification notification)
        {
            if (notification == null || string.IsNullOrEmpty(notification.Path) || string.IsNullOrEmpty(notification.RepoRoot))
            {
                return;
            }

            if (!TryAccept(notification.Id, notification.RepoRoot, notification.Path, notification.Result?.GitBlobSha, out var absolutePath, out var content))
            {
                return;
            }

            var review = _mapper.Map(absolutePath, notification.Result);
            new ReviewCacheService().Put(new ReviewCacheEntry(content, absolutePath, review));
            ReviewApplied?.Invoke(this, new FileReviewAppliedEventArgs(absolutePath, review, notification.Id));
        }

        private void OnDeltaReview(object sender, DeltaReviewNotification notification)
        {
            if (notification == null || string.IsNullOrEmpty(notification.Path) || string.IsNullOrEmpty(notification.RepoRoot))
            {
                return;
            }

            var sha = notification.Result?.NewGitBlobSha;
            if (!TryAccept(notification.Id, notification.RepoRoot, notification.Path, sha, out var absolutePath, out var content))
            {
                return;
            }

            var cache = new DeltaCacheService();
            if (IsEmptyDelta(notification.Result))
            {
                cache.Invalidate(absolutePath);
            }
            else
            {
                cache.Put(new DeltaCacheEntry(absolutePath, string.Empty, content, notification.Result));
            }

            _notifier?.OnDeltaCompleted(absolutePath);
            _notifier?.RequestViewUpdate();
            DeltaApplied?.Invoke(this, new DeltaReviewAppliedEventArgs(absolutePath, notification.Result, notification.Id));
        }

        private void OnReviewFailed(object sender, ReviewFailedNotification notification)
        {
            if (notification == null || string.IsNullOrEmpty(notification.Path) || string.IsNullOrEmpty(notification.RepoRoot))
            {
                return;
            }

            if (!string.IsNullOrEmpty(notification.Id) && !_pendingById.TryRemove(notification.Id, out _))
            {
                return;
            }

            var absolutePath = RepoPathUtil.ToAbsolutePath(notification.RepoRoot, notification.Path);
            _notifier?.OnDeltaCompleted(absolutePath);
            ReviewFailed?.Invoke(this, new ReviewFailedEventArgs(absolutePath, notification.Message, notification.Id));
        }

        private bool TryAccept(string id, string repoRoot, string relPath, string resultSha, out string absolutePath, out string content)
        {
            absolutePath = RepoPathUtil.ToAbsolutePath(repoRoot, relPath);
            content = null;

            if (!string.IsNullOrEmpty(id))
            {
                if (!_pendingById.TryRemove(id, out var pending))
                {
                    return false;
                }

                if (_latestIdByPath.TryGetValue(PathKey(repoRoot, relPath), out var latest) &&
                    !string.Equals(latest, id, StringComparison.Ordinal))
                {
                    return false;
                }

                absolutePath = pending.AbsolutePath;
                var live = GetLiveContent(absolutePath);
                content = string.IsNullOrEmpty(live) ? pending.Content : live;
            }
            else
            {
                content = GetLiveContent(absolutePath);
            }

            var liveSha = string.IsNullOrEmpty(id)
                ? GitBlobShaFromDisk(absolutePath, content)
                : GitBlobSha.FromUtf8(content ?? string.Empty);
            if (!MatchesHash(resultSha, liveSha))
            {
                _logger.Debug($"Discarding stale review for {absolutePath}.");
                return false;
            }

            return true;
        }

        private string GetLiveContent(string absolutePath)
        {
            try
            {
                if (_openDocumentContentProvider != null)
                {
                    var fromBuffer = _openDocumentContentProvider.GetContentForReviewAsync(absolutePath).GetAwaiter().GetResult();
                    if (fromBuffer != null)
                    {
                        return fromBuffer;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Debug($"Could not read open document content for {absolutePath}: {ex.Message}");
            }

            try
            {
                return File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GitBlobShaFromDisk(string absolutePath, string content)
        {
            try
            {
                if (File.Exists(absolutePath))
                {
                    return GitBlobSha.FromBytes(File.ReadAllBytes(absolutePath));
                }
            }
            catch
            {
            }

            return GitBlobSha.FromUtf8(content ?? string.Empty);
        }

        private static bool MatchesHash(string resultSha, string liveSha)
        {
            if (string.IsNullOrEmpty(resultSha))
            {
                return true;
            }

            return string.Equals(resultSha, liveSha, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEmptyDelta(DeltaResponseModel delta)
        {
            if (delta == null)
            {
                return true;
            }

            var fileFindings = delta.FileLevelFindings == null || delta.FileLevelFindings.Length == 0;
            var fnFindings = delta.FunctionLevelFindings == null || delta.FunctionLevelFindings.Length == 0;
            return fileFindings && fnFindings && delta.ScoreChange == 0;
        }

        private static string PathKey(string repoRoot, string relPath)
        {
            return (repoRoot ?? string.Empty) + "|" + (relPath ?? string.Empty);
        }

        private sealed class PendingReview
        {
            public string Id { get; set; }

            public string RepoRoot { get; set; }

            public string RelPath { get; set; }

            public string AbsolutePath { get; set; }

            public string Content { get; set; }

            public string ContentHash { get; set; }
        }
    }
}
