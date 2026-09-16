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
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Git
{
    [Export(typeof(IWorkspaceWatchCoordinator))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class WorkspaceWatchCoordinator : IWorkspaceWatchCoordinator, IDisposable
    {
        internal const int InventoryRefreshDelayMs = 250;
        private readonly IIdeServerClient _client;
        private readonly IReviewPipeline _pipeline;
        private readonly ILogger _logger;
        private readonly IWorkspaceWatchHost _host;
        private readonly object _gate = new object();
        private readonly Dictionary<string, string> _watched = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _inventories = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Timer> _refreshTimers = new Dictionary<string, Timer>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _knownRoots = new Dictionary<string, string>(StringComparer.Ordinal);
        private bool _disposed;

        [ImportingConstructor]
        public WorkspaceWatchCoordinator(
            IIdeServerClient client,
            IReviewPipeline pipeline,
            ILogger logger,
            [Import(AllowDefault = true)] IWorkspaceWatchHost host = null)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            _logger = logger;
            _host = host;
            _client.WatchInventoryChanged += OnWatchInventoryChanged;
            _client.ServerStarted += OnServerStarted;
            _client.DeltaReceived += OnDeltaReceived;
        }

        public Task SyncAsync(IReadOnlyList<string> workspaceDirectories)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            var targets = ResolveTargets(workspaceDirectories ?? Array.Empty<string>());
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
                    }
                }
            }

            foreach (var target in targets)
            {
                EnsureWatch(target, ScopePaths(target, workspaceDirectories));
            }

            return Task.CompletedTask;
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
                foreach (var timer in _refreshTimers.Values)
                {
                    timer.Dispose();
                }

                _refreshTimers.Clear();
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
            _client.DeltaReceived -= OnDeltaReceived;
            StopAll();
            lock (_gate)
            {
                foreach (var timer in _refreshTimers.Values)
                {
                    timer.Dispose();
                }

                _refreshTimers.Clear();
            }
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

        private void EnsureWatch(string repoRoot, IReadOnlyList<string> relativePaths)
        {
            var key = RpcPath.NormalizeFsPath(repoRoot);
            var scopeKey = relativePaths == null ? "*" : string.Join("|", relativePaths);
            lock (_gate)
            {
                if (_watched.TryGetValue(key, out var existingScope) && existingScope == scopeKey)
                {
                    Seed(repoRoot);
                    return;
                }

                _client.WatchFiles(repoRoot, relativePaths);
                _watched[key] = scopeKey;
                PublishActiveReposUnlocked();
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
            _client.StopWatchFiles(repoRoot);
            _watched.Remove(normalizedRoot);
            PublishActiveReposUnlocked();
            ApplyInventoryUnlocked(repoRoot, Array.Empty<string>());
        }

        private void OnWatchInventoryChanged(object sender, WatchInventory inventory)
        {
            if (inventory == null)
            {
                return;
            }

            lock (_gate)
            {
                var key = RpcPath.NormalizeFsPath(ResolveRepoRoot(inventory.RepoRoot));
                if (!_watched.ContainsKey(key))
                {
                    return;
                }

                ApplyInventoryUnlocked(inventory.RepoRoot, inventory.Files ?? Array.Empty<string>());
            }
        }

        private void ApplyInventoryUnlocked(string repoRoot, IReadOnlyList<string> files)
        {
            var resolved = ResolveRepoRoot(repoRoot);
            var key = RpcPath.NormalizeFsPath(resolved);
            var relPaths = new HashSet<string>(files.Select(RpcPath.ToPosixRelPath), StringComparer.Ordinal);
            _inventories[key] = relPaths;
            var keep = PathsToKeepUnlocked();
            _host?.PruneMonitor(_inventories.Keys.Select(ResolveRepoRoot).ToList(), keep);
        }

        private void OnDeltaReceived(object sender, DeltaNotification notification)
        {
            if (notification?.Result == null)
            {
                return;
            }

            lock (_gate)
            {
                var repoRoot = ResolveRepoRoot(notification.RepoRoot);
                var key = RpcPath.NormalizeFsPath(repoRoot);
                if (!_inventories.TryGetValue(key, out var inventory))
                {
                    return;
                }

                var relPath = RpcPath.ToPosixRelPath(notification.Path);
                if (inventory.Contains(relPath) || IsDirty(repoRoot, relPath))
                {
                    return;
                }

                ScheduleInventoryRefresh(repoRoot, key);
            }
        }

        private void ScheduleInventoryRefresh(string repoRoot, string key)
        {
            if (_refreshTimers.TryGetValue(key, out var existing))
            {
                existing.Change(InventoryRefreshDelayMs, Timeout.Infinite);
                return;
            }

            _refreshTimers[key] = new Timer(
                _ =>
                {
                    lock (_gate)
                    {
                        if (_refreshTimers.TryGetValue(key, out var timer))
                        {
                            timer.Dispose();
                            _refreshTimers.Remove(key);
                        }
                    }

                    _ = RefreshInventoryAsync(repoRoot);
                },
                null,
                InventoryRefreshDelayMs,
                Timeout.Infinite);
        }

        private async Task RefreshInventoryAsync(string repoRoot)
        {
            try
            {
                var inventory = await _client.GetWatchInventoryAsync(repoRoot).ConfigureAwait(false);
                lock (_gate)
                {
                    ApplyInventoryUnlocked(repoRoot, inventory?.Files ?? Array.Empty<string>());
                }
            }
            catch (Exception ex)
            {
                _logger?.Debug($"[watch] inventory refresh skipped for {repoRoot}: {ex.Message}");
            }
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

            return startEvent.Restart;
        }

        private void Seed(string repoRoot)
        {
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

            foreach (var document in _host?.GetDirtyDocuments() ?? Array.Empty<ReviewDocument>())
            {
                keep.Add(document.FilePath);
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
