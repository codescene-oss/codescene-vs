// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(IReviewPipeline))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed partial class ReviewPipeline : IReviewPipeline, IDisposable
    {
        private const int MaxWatchReviews = 250;
        private readonly IIdeServerClient _client;
        private readonly IReviewPipelinePresentation _presentation;
        private readonly IReviewPipelineFileAccess _fileAccess;
        private readonly Func<string> _createId;
        private readonly ILogger _logger;
        private readonly ISettingsProvider _settings;
        private readonly object _gate = new object();
        private readonly HashSet<string> _bufferOwnedPaths = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingReview> _pendingById = new Dictionary<string, PendingReview>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingReview> _latestByPath = new Dictionary<string, PendingReview>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _tombstones = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _generations = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, WatchReviewEntry> _watchReviews = new Dictionary<string, WatchReviewEntry>(StringComparer.Ordinal);
        private readonly List<string> _watchReviewOrder = new List<string>();
        private int _dedupEpoch;
        private HashSet<string> _activeRepos;
        private bool _disposed;
        private Func<string, string, Task<bool>> _watchDeltaFilter = (_, __) => Task.FromResult(true);

        [ImportingConstructor]
        public ReviewPipeline(
            IIdeServerClient client,
            [Import(AllowDefault = true)] IReviewPipelinePresentation presentation = null,
            [Import(AllowDefault = true)] IReviewPipelineFileAccess fileAccess = null,
            [Import(AllowDefault = true)] ILogger logger = null,
            [Import(AllowDefault = true)] ISettingsProvider settings = null)
            : this(client, presentation, fileAccess, null, logger, settings)
        {
        }

        internal ReviewPipeline(
            IIdeServerClient client,
            IReviewPipelinePresentation presentation,
            IReviewPipelineFileAccess fileAccess,
            Func<string> createId,
            ILogger logger,
            ISettingsProvider settings = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _presentation = presentation ?? NoOpPresentation.Instance;
            _fileAccess = fileAccess ?? DiskFileAccess.Instance;
            _createId = createId ?? (() => Guid.NewGuid().ToString("N"));
            _logger = logger;
            _settings = settings;
            _client.ReviewReceived += OnReviewReceived;
            _client.DeltaReceived += OnDeltaReceived;
            _client.ReviewFailed += OnReviewFailed;
            _client.ServerError += OnServerError;
        }

        public async Task<(CliReviewModel Review, DeltaResponseModel Delta)> SubmitAsync(string repoRoot, ReviewSubmission submission)
        {
            var results = await SubmitBatchInternalAsync(repoRoot, new[] { submission }).ConfigureAwait(false);
            return results[0];
        }

        public Task SubmitBatchAsync(string repoRoot, ReviewSubmission[] submissions)
        {
            return SubmitBatchInternalAsync(repoRoot, submissions ?? Array.Empty<ReviewSubmission>());
        }

        public void Remove(string repoRoot, params ReviewDocument[] documents)
        {
            if (documents == null)
            {
                return;
            }

            lock (_gate)
            {
                foreach (var document in documents)
                {
                    if (document == null)
                    {
                        continue;
                    }

                    var pathKey = FindPathKey(repoRoot, document);
                    if (_latestByPath.TryGetValue(pathKey, out var latest))
                    {
                        IgnorePending(latest);
                    }

                    var generation = NextGeneration(pathKey);
                    _tombstones[pathKey] = generation;
                    _latestByPath.Remove(pathKey);
                    RemoveWatchReview(pathKey);
                    _bufferOwnedPaths.Remove(pathKey);
                    _presentation.Remove(document);
                }
            }
        }

        public void ReleaseBufferMonitorOwnership(string repoRoot, ReviewDocument document)
        {
            if (document == null)
            {
                return;
            }

            lock (_gate)
            {
                _bufferOwnedPaths.Remove(PathKeyForDocument(repoRoot, document));
            }
        }

        public void RestoreFromDiskIfBufferOwned(string repoRoot, ReviewDocument document)
        {
            if (document == null || string.IsNullOrEmpty(document.FilePath))
            {
                return;
            }

            var owned = false;
            lock (_gate)
            {
                owned = _bufferOwnedPaths.Contains(PathKeyForDocument(repoRoot, document));
            }

            if (!owned)
            {
                return;
            }

            Remove(repoRoot, document);
            _ = SubmitAsync(repoRoot, new ReviewSubmission
            {
                Document = document,
                RelPath = RpcPath.RelativePosix(repoRoot, document.FilePath),
                UpdateDiagnosticsPane = false,
                UpdateMonitor = true,
            });
        }

        public void SetWatchDeltaFilter(Func<string, string, Task<bool>> filter)
        {
            _watchDeltaFilter = filter ?? ((_, __) => Task.FromResult(true));
        }

        public void Invalidate()
        {
            lock (_gate)
            {
                _dedupEpoch++;
                _logger?.Debug("[pipeline] epochInvalidated dedupEpoch=" + _dedupEpoch);
            }
        }

        public void Reset()
        {
            lock (_gate)
            {
                foreach (var pending in _pendingById.Values.ToList())
                {
                    CompletePending(pending);
                }

                _dedupEpoch++;
                _latestByPath.Clear();
                _tombstones.Clear();
                _watchReviews.Clear();
                _watchReviewOrder.Clear();
                _bufferOwnedPaths.Clear();
            }
        }

        public void SetActiveRepos(IReadOnlyCollection<string> repoRoots)
        {
            lock (_gate)
            {
                _activeRepos = repoRoots == null
                    ? null
                    : new HashSet<string>(repoRoots.Select(RpcPath.NormalizeFsPath), StringComparer.Ordinal);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _client.ReviewReceived -= OnReviewReceived;
            _client.DeltaReceived -= OnDeltaReceived;
            _client.ReviewFailed -= OnReviewFailed;
            _client.ServerError -= OnServerError;
            FailAll(new Exception("Review pipeline stopped"));
            lock (_gate)
            {
                _latestByPath.Clear();
                _tombstones.Clear();
                _watchReviews.Clear();
                _watchReviewOrder.Clear();
                _bufferOwnedPaths.Clear();
            }
        }

        private static bool MatchesHash(string received, string expected)
        {
            return received == null || received == expected;
        }

        private static string PathKey(string repoRoot, string relPath)
        {
            return RpcPath.NormalizeFsPath(repoRoot) + "\0" + RpcPath.ToPosixRelPath(relPath);
        }

        private static string PathKeyForDocument(string repoRoot, ReviewDocument document)
        {
            return PathKey(repoRoot, RpcPath.RelativePosix(repoRoot, document.FilePath));
        }

        private static PresentedReview ToPresentedReview(PendingReview pending, CliReviewModel result)
        {
            var presented = CopySubmission<PresentedReview>(pending);
            presented.Result = result;
            return presented;
        }

        private static PresentedDelta ToPresentedDelta(PendingReview pending, DeltaResponseModel result)
        {
            var presented = CopySubmission<PresentedDelta>(pending);
            presented.Result = result;
            return presented;
        }

        private static T CopySubmission<T>(PendingReview pending)
            where T : ReviewSubmission, new()
        {
            return new T
            {
                Document = pending.Document,
                RelPath = pending.RelPath,
                Content = pending.Content,
                UpdateDiagnosticsPane = pending.UpdateDiagnosticsPane,
                UpdateMonitor = pending.UpdateMonitor,
            };
        }

        private bool AutomaticAnalysisEnabled()
        {
            return _settings?.EnableAutomaticAnalysis ?? true;
        }

        private Task<(CliReviewModel Review, DeltaResponseModel Delta)[]> SubmitBatchInternalAsync(string repoRoot, IReadOnlyList<ReviewSubmission> submissions)
        {
            var newReviews = new List<PendingReview>();
            var diskFiles = new List<ReviewFile>();
            var tasks = new Task<(CliReviewModel, DeltaResponseModel)>[submissions.Count];

            lock (_gate)
            {
                for (var i = 0; i < submissions.Count; i++)
                {
                    var submission = submissions[i];
                    if (submission.Content == null)
                    {
                        var relPath = RpcPath.ToPosixRelPath(submission.RelPath);
                        diskFiles.Add(new ReviewFile { RelPath = relPath });
                        _logger?.Debug("[pipeline] submit " + LogFields.Format(
                            LogFields.Field("path", relPath),
                            LogFields.Field("repo", repoRoot),
                            LogFields.Field("source", "disk"),
                            LogFields.Field("updateMonitor", submission.UpdateMonitor),
                            LogFields.Field("updateDiagnostics", submission.UpdateDiagnosticsPane)));
                        tasks[i] = Task.FromResult<(CliReviewModel, DeltaResponseModel)>((null, null));
                        continue;
                    }

                    var pending = PrepareSubmission(repoRoot, submission);
                    if (!pending.Submitted)
                    {
                        _pendingById[pending.Id] = pending;
                        pending.Submitted = true;
                        newReviews.Add(pending);
                    }

                    tasks[i] = pending.Completion.Task;
                }
            }

            var files = newReviews
                .Select(pending => new ReviewFile { Id = pending.Id, RelPath = pending.RelPath, Content = pending.Content })
                .Concat(diskFiles)
                .ToList();
            if (files.Count > 0)
            {
                _logger?.Debug("[pipeline] submitBatch " + LogFields.Format(
                    LogFields.Field("repo", repoRoot),
                    LogFields.Field("count", files.Count),
                    LogFields.Field("buffer", newReviews.Count),
                    LogFields.Field("disk", diskFiles.Count)));
                _client.ReviewFiles(repoRoot, files);
            }

            return Task.WhenAll(tasks);
        }

        private PendingReview PrepareSubmission(string repoRoot, ReviewSubmission submission)
        {
            if (submission.Document == null)
            {
                throw new InvalidOperationException("Buffer reviews require a document");
            }

            var normalized = new ReviewSubmission
            {
                Document = submission.Document,
                RelPath = RpcPath.ToPosixRelPath(submission.RelPath),
                Content = submission.Content,
                UpdateDiagnosticsPane = submission.UpdateDiagnosticsPane,
                UpdateMonitor = submission.UpdateMonitor,
            };
            var pathKey = PathKey(repoRoot, normalized.RelPath);
            var contentHash = GitBlobSha.FromUtf8(normalized.Content);
            var context = new SubmissionContext
            {
                RepoRoot = repoRoot,
                PathKey = pathKey,
                ContentHash = contentHash,
                DedupKey = pathKey + "\0" + contentHash + "\0" + _dedupEpoch,
            };

            _latestByPath.TryGetValue(pathKey, out var latest);
            if (latest != null && latest.DedupKey == context.DedupKey)
            {
                _logger?.Debug("[pipeline] submit reused pending " + LogFields.Format(
                    LogFields.Field("id", latest.Id),
                    LogFields.Field("path", normalized.RelPath),
                    LogFields.Field("repo", repoRoot),
                    LogFields.Field("reason", "dedup")));
                MergePresentation(latest, normalized);
                RememberBufferOwnership(pathKey, latest.UpdateMonitor);
                return latest;
            }

            var reused = ReusedWatchReview(context, normalized, latest);
            if (reused != null)
            {
                return reused;
            }

            if (latest != null)
            {
                IgnorePending(latest);
            }

            var pending = CreatePending(context, normalized);
            _logger?.Debug("[pipeline] submit " + LogFields.Format(
                LogFields.Field("id", pending.Id),
                LogFields.Field("path", pending.RelPath),
                LogFields.Field("repo", repoRoot),
                LogFields.Field("source", "buffer"),
                LogFields.Field("updateMonitor", pending.UpdateMonitor),
                LogFields.Field("updateDiagnostics", pending.UpdateDiagnosticsPane)));
            _tombstones.Remove(pathKey);
            _latestByPath[pathKey] = pending;
            RememberBufferOwnership(pathKey, pending.UpdateMonitor);
            _presentation.ReviewStarted(normalized.Document);
            _presentation.DeltaStarted(normalized.Document);
            return pending;
        }

        private PendingReview CreatePending(SubmissionContext context, ReviewSubmission submission)
        {
            return new PendingReview
            {
                Document = submission.Document,
                RelPath = submission.RelPath,
                Content = submission.Content,
                UpdateDiagnosticsPane = submission.UpdateDiagnosticsPane,
                UpdateMonitor = submission.UpdateMonitor,
                Id = _createId(),
                RepoRoot = RpcPath.NormalizeFsPath(context.RepoRoot),
                DedupKey = context.DedupKey,
                PathKey = context.PathKey,
                ContentHash = context.ContentHash,
                Generation = NextGeneration(context.PathKey),
                Completion = new TaskCompletionSource<(CliReviewModel, DeltaResponseModel)>(TaskCreationOptions.RunContinuationsAsynchronously),
            };
        }

        private PendingReview ReusedWatchReview(SubmissionContext context, ReviewSubmission submission, PendingReview latest)
        {
            if (latest != null)
            {
                return null;
            }

            if (submission.UpdateMonitor)
            {
                return null;
            }

            var cached = CachedWatchReview(context);
            if (cached == null)
            {
                return null;
            }

            var pending = CreatePending(context, submission);
            pending.Submitted = true;
            pending.ReviewDone = true;
            pending.DeltaDone = true;
            pending.ReviewResult = cached;
            pending.Completion.TrySetResult((cached, null));
            if (submission.UpdateDiagnosticsPane)
            {
                _presentation.PresentReview(ToPresentedReview(pending, cached));
            }

            return pending;
        }

        private CliReviewModel CachedWatchReview(SubmissionContext context)
        {
            if (!_watchReviews.TryGetValue(context.PathKey, out var cached))
            {
                return null;
            }

            if (cached.ContentHash != context.ContentHash || cached.DedupEpoch != _dedupEpoch)
            {
                return null;
            }

            return cached.Result;
        }

        private void MergePresentation(PendingReview pending, ReviewSubmission submission)
        {
            var replayReview = ShouldReplay(pending.UpdateDiagnosticsPane, submission.UpdateDiagnosticsPane);
            var replayDelta = ShouldReplay(pending.UpdateMonitor, submission.UpdateMonitor);
            pending.UpdateDiagnosticsPane |= submission.UpdateDiagnosticsPane;
            pending.UpdateMonitor |= submission.UpdateMonitor;
            ReplayReview(pending, replayReview);
            ReplayDelta(pending, replayDelta);
        }

        private bool ShouldReplay(bool alreadyEnabled, bool requested)
        {
            return !alreadyEnabled && requested;
        }

        private void ReplayReview(PendingReview pending, bool replay)
        {
            if (replay && pending.ReviewResult != null)
            {
                _presentation.PresentReview(ToPresentedReview(pending, pending.ReviewResult));
            }
        }

        private void ReplayDelta(PendingReview pending, bool replay)
        {
            if (replay && pending.DeltaDone)
            {
                PresentOwnedDelta(pending);
            }
        }

        private void PresentOwnedDelta(PendingReview pending)
        {
            if (AutomaticAnalysisEnabled())
            {
                _presentation.PresentDelta(ToPresentedDelta(pending, pending.DeltaResult));
            }
        }

        private void RememberBufferOwnership(string pathKey, bool updateMonitor)
        {
            if (updateMonitor)
            {
                _bufferOwnedPaths.Add(pathKey);
            }
        }

        private string FindPathKey(string repoRoot, ReviewDocument document)
        {
            foreach (var pair in _latestByPath)
            {
                if (RpcPath.PathsEqual(pair.Value.Document.FilePath, document.FilePath))
                {
                    return pair.Key;
                }
            }

            return PathKey(repoRoot, RpcPath.RelativePosix(repoRoot, document.FilePath));
        }

        private int NextGeneration(string pathKey)
        {
            _generations.TryGetValue(pathKey, out var generation);
            generation++;
            _generations[pathKey] = generation;
            return generation;
        }
    }
}
