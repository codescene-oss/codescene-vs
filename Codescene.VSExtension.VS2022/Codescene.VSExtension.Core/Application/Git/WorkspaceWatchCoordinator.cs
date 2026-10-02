// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Git
{
    [Export(typeof(IWorkspaceWatchCoordinator))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed partial class WorkspaceWatchCoordinator : IWorkspaceWatchCoordinator, IDisposable
    {
        internal const int InventoryRefreshDelayMs = 250;
        internal const int QueueIdlePruneDelayMs = 1000;
        private readonly IIdeServerClient _client;
        private readonly IReviewPipeline _pipeline;
        private readonly ILogger _logger;
        private readonly IWorkspaceWatchHost _host;
        private readonly object _gate = new object();
        private readonly Dictionary<string, string> _watched = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _inventories = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingRefresh> _pendingRefreshes = new Dictionary<string, PendingRefresh>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _knownRoots = new Dictionary<string, string>(StringComparer.Ordinal);
        private IReadOnlyList<string> _workspaceDirectories = Array.Empty<string>();
        private Timer _queueIdleTimer;
        private volatile bool _enabled = true;
        private bool _disposed;

        [ImportingConstructor]
        public WorkspaceWatchCoordinator(
            IIdeServerClient client,
            IReviewPipeline pipeline,
            ILogger logger,
            [Import(AllowDefault = true)] IWorkspaceWatchHost host = null,
            [Import(AllowDefault = true)] ISettingsProvider settings = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _logger = logger;
            _host = host;
            _enabled = settings?.EnableAutomaticAnalysis ?? true;
            _pipeline.SetWatchDeltaFilter((repoRoot, relPath) => AdmitsDeltaAsync(repoRoot, relPath));
            _client.WatchInventoryChanged += OnWatchInventoryChanged;
            _client.ServerStarted += OnServerStarted;
            _client.QueueChanged += OnQueueChanged;
        }

        public Task SyncAsync(IReadOnlyList<string> workspaceDirectories)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            _workspaceDirectories = workspaceDirectories ?? Array.Empty<string>();
            if (!_enabled)
            {
                return Task.CompletedTask;
            }

            var targets = ResolveTargets(_workspaceDirectories);
            _logger?.Debug("[watch] syncing " + LogFields.Format(LogFields.Field("count", targets.Count)));
            if (ReplaceWatchedRoots(targets))
            {
                PruneMonitor();
            }

            foreach (var target in targets)
            {
                EnsureWatch(target, ScopePaths(target, workspaceDirectories));
            }

            return Task.CompletedTask;
        }

        public void SetEnabled(bool enabled)
        {
            if (_disposed || _enabled == enabled)
            {
                return;
            }

            _enabled = enabled;
            if (enabled)
            {
                _logger?.Info("[watch] automatic analysis started");
                _ = SyncAsync(_workspaceDirectories);
                return;
            }

            _logger?.Info("[watch] automatic analysis stopped");
            PauseWatching();
        }

        public void StopAll()
        {
            lock (_gate)
            {
                foreach (var repoRoot in _watched.Keys.ToList())
                {
                    StopWatchingUnlocked(repoRoot);
                }

                _inventories.Clear();
                _knownRoots.Clear();
                CancelPendingRefreshesUnlocked();
                _pipeline.SetActiveRepos(Array.Empty<string>());
            }

            _pipeline.Reset();
            _host?.PruneMonitor(Array.Empty<string>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        public IReadOnlyCollection<string> GetInventoryFiles(string repoRoot)
        {
            lock (_gate)
            {
                if (!_inventories.TryGetValue(RpcPath.NormalizeFsPath(repoRoot), out var files))
                {
                    return Array.Empty<string>();
                }

                return files.ToList();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _client.WatchInventoryChanged -= OnWatchInventoryChanged;
            _client.ServerStarted -= OnServerStarted;
            _client.QueueChanged -= OnQueueChanged;
            StopAll();
        }

        private static List<string> ResolveTargets(IReadOnlyList<string> directories)
        {
            var targets = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var directory in directories)
            {
                var repoRoot = GitPathDiscovery.TryGetWorkingDirectory(directory);
                if (string.IsNullOrEmpty(repoRoot))
                {
                    continue;
                }

                targets[RpcPath.NormalizeFsPath(repoRoot)] = repoRoot;
            }

            return targets.Values.ToList();
        }

        private static IReadOnlyList<string> ScopePaths(string repoRoot, IReadOnlyList<string> workspaceDirectories)
        {
            var relative = new List<string>();
            foreach (var directory in workspaceDirectories ?? Array.Empty<string>())
            {
                if (!RpcPath.PathsEqual(GitPathDiscovery.TryGetWorkingDirectory(directory) ?? string.Empty, repoRoot))
                {
                    continue;
                }

                var posix = RpcPath.RelativePosix(repoRoot, directory);
                if (IsNestedScope(posix))
                {
                    relative.Add(posix);
                }
            }

            return CoversEntireRepo(relative) ? null : relative;
        }

        private static bool IsNestedScope(string posix)
        {
            if (string.IsNullOrEmpty(posix) || posix == ".")
            {
                return false;
            }

            return !posix.StartsWith("..", StringComparison.Ordinal);
        }

        private static bool CoversEntireRepo(IReadOnlyList<string> relative)
        {
            return relative.Count == 0 || relative.Any(path => string.IsNullOrEmpty(path) || path == ".");
        }

        private bool ReplaceWatchedRoots(IReadOnlyList<string> targets)
        {
            var stopped = false;
            lock (_gate)
            {
                _knownRoots.Clear();
                foreach (var target in targets)
                {
                    _knownRoots[RpcPath.NormalizeFsPath(target)] = target;
                }

                var keep = new HashSet<string>(targets.Select(RpcPath.NormalizeFsPath), StringComparer.Ordinal);
                foreach (var existing in _watched.Keys.ToList())
                {
                    if (!keep.Contains(existing))
                    {
                        StopWatchingUnlocked(existing);
                        stopped = true;
                    }
                }
            }

            return stopped;
        }

        private void EnsureWatch(string repoRoot, IReadOnlyList<string> relativePaths)
        {
            var key = RpcPath.NormalizeFsPath(repoRoot);
            var scopeKey = relativePaths == null ? "*" : string.Join("|", relativePaths);
            lock (_gate)
            {
                if (_watched.TryGetValue(key, out var existingScope) && existingScope == scopeKey)
                {
                    _logger?.Debug("[watch] ensureWatch " + LogFields.Format(
                        LogFields.Field("action", "skip"),
                        LogFields.Field("repo", repoRoot),
                        LogFields.Field("reason", "scope-unchanged"),
                        LogFields.Field("scopeKey", scopeKey)));
                }
                else
                {
                    _logger?.Info("[watch] ensureWatch " + LogFields.Format(
                        LogFields.Field("action", "startWatch"),
                        LogFields.Field("repo", repoRoot),
                        LogFields.Field("paths", relativePaths ?? (IReadOnlyList<string>)new[] { "(whole repository)" })));
                    _client.WatchFiles(repoRoot, relativePaths);
                    _watched[key] = scopeKey;
                    PublishActiveReposUnlocked();
                }
            }

            Seed(repoRoot);
        }

        private void StopWatchingUnlocked(string normalizedRoot)
        {
            if (!_watched.TryGetValue(normalizedRoot, out var _))
            {
                return;
            }

            var repoRoot = _knownRoots.TryGetValue(normalizedRoot, out var known) ? known : normalizedRoot;
            _logger?.Info("[watch] stopped " + LogFields.Format(
                LogFields.Field("repo", repoRoot),
                LogFields.Field("reason", "unspecified")));
            _client.StopWatchFiles(repoRoot);
            _watched.Remove(normalizedRoot);
            PublishActiveReposUnlocked();
            ApplyInventoryUnlocked(repoRoot, Array.Empty<string>());
        }

        private void OnWatchInventoryChanged(object sender, WatchInventory inventory)
        {
            if (inventory == null || !_enabled)
            {
                return;
            }

            var applied = false;
            lock (_gate)
            {
                var key = RpcPath.NormalizeFsPath(ResolveRepoRoot(inventory.RepoRoot));
                if (!_watched.ContainsKey(key))
                {
                    return;
                }

                applied = ApplyInventoryUnlocked(inventory.RepoRoot, inventory.Files ?? Array.Empty<string>());
            }

            if (applied)
            {
                PruneMonitor();
            }
        }

        private bool ApplyInventoryUnlocked(string repoRoot, IReadOnlyList<string> files)
        {
            if (!_enabled)
            {
                return false;
            }

            var resolved = ResolveRepoRoot(repoRoot);
            var key = RpcPath.NormalizeFsPath(resolved);
            var relPaths = new HashSet<string>(files.Select(RpcPath.ToPosixRelPath), StringComparer.Ordinal);
            _inventories[key] = relPaths;
            _logger?.Debug("[watch] inventory applied " + LogFields.Format(
                LogFields.Field("repo", resolved),
                LogFields.Field("files", relPaths.Count)));
            return true;
        }

        private void PruneMonitor()
        {
            IReadOnlyList<string> repoRoots;
            ISet<string> keep;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                repoRoots = _inventories.Keys.Select(ResolveRepoRoot).ToList();
                keep = PathsToKeepUnlocked();
            }

            foreach (var document in _host?.GetDirtyDocuments() ?? Array.Empty<ReviewDocument>())
            {
                keep.Add(document.FilePath);
            }

            _host?.PruneMonitor(repoRoots, keep);
        }

        private void OnServerStarted(object sender, ServerStartEvent startEvent)
        {
            if (!ShouldRestoreWatches(startEvent))
            {
                return;
            }

            _logger?.Info("[watch] cs-ide restarted, re-establishing repository watches");
            lock (_gate)
            {
                _watched.Clear();
            }

            _ = SyncAsync(_knownRoots.Values.ToList());
        }

        private bool ShouldRestoreWatches(ServerStartEvent startEvent)
        {
            if (startEvent == null || _disposed)
            {
                return false;
            }

            if (!_enabled)
            {
                return false;
            }

            return startEvent.Restart;
        }

        private void Seed(string repoRoot)
        {
            if (!_enabled)
            {
                return;
            }

            var submissions = DirtySubmissions(repoRoot);
            if (submissions.Count == 0)
            {
                return;
            }

            _logger?.Info($"[watch] seeding reviewFiles count={submissions.Count} repo={repoRoot}");
            _ = _pipeline.SubmitBatchAsync(repoRoot, submissions.ToArray());
        }

        private List<ReviewSubmission> DirtySubmissions(string repoRoot)
        {
            var submissions = new List<ReviewSubmission>();
            foreach (var document in _host?.GetDirtyDocuments() ?? Array.Empty<ReviewDocument>())
            {
                if (!RpcPath.PathsEqual(GitPathDiscovery.TryGetWorkingDirectory(document.FilePath) ?? string.Empty, repoRoot))
                {
                    continue;
                }

                submissions.Add(new ReviewSubmission
                {
                    Document = document,
                    RelPath = RpcPath.RelativePosix(repoRoot, document.FilePath),
                    Content = document.Content,
                    UpdateDiagnosticsPane = true,
                    UpdateMonitor = true,
                });
            }

            return submissions;
        }

        private bool IsDirty(string repoRoot, string relPath)
        {
            foreach (var document in _host?.GetDirtyDocuments() ?? Array.Empty<ReviewDocument>())
            {
                if (RpcPath.ToPosixRelPath(RpcPath.RelativePosix(repoRoot, document.FilePath)) == relPath)
                {
                    return true;
                }
            }

            return false;
        }

        private ISet<string> PathsToKeepUnlocked()
        {
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in _inventories)
            {
                var repoRoot = ResolveRepoRoot(pair.Key);
                foreach (var relPath in pair.Value)
                {
                    keep.Add(RpcPath.CombineRepo(repoRoot, relPath));
                }
            }

            return keep;
        }

        private void PublishActiveReposUnlocked()
        {
            _pipeline.SetActiveRepos(_watched.Keys.Select(ResolveRepoRoot).ToList());
        }

        private string ResolveRepoRoot(string reported)
        {
            return _knownRoots.TryGetValue(RpcPath.NormalizeFsPath(reported), out var known)
                ? known
                : Path.GetFullPath(reported ?? string.Empty);
        }
    }
}
