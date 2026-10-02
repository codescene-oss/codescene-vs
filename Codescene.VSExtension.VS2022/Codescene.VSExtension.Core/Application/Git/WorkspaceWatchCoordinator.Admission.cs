// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Git
{
    public sealed partial class WorkspaceWatchCoordinator
    {
        internal async Task<bool> AdmitsDeltaAsync(string reportedRoot, string relPath)
        {
            if (_disposed || !_enabled)
            {
                return true;
            }

            string repoRoot;
            string posixPath;
            string key;
            Task refresh;
            lock (_gate)
            {
                repoRoot = ResolveRepoRoot(reportedRoot);
                posixPath = RpcPath.ToPosixRelPath(relPath);
                key = RpcPath.NormalizeFsPath(repoRoot);
                if (!_inventories.ContainsKey(key))
                {
                    _logger?.Debug("[watch] delta admitted " + LogFields.Format(
                        LogFields.Field("repo", repoRoot),
                        LogFields.Field("path", posixPath),
                        LogFields.Field("reason", "no-inventory")));
                    return true;
                }

                if (IsMonitoredPathUnlocked(repoRoot, posixPath))
                {
                    return true;
                }

                _logger?.Debug("[watch] delta reconcile scheduled " + LogFields.Format(
                    LogFields.Field("repo", repoRoot),
                    LogFields.Field("path", posixPath),
                    LogFields.Field("reason", "not-in-inventory")));
                refresh = ScheduleInventoryRefreshUnlocked(repoRoot, key);
            }

            await refresh.ConfigureAwait(false);
            lock (_gate)
            {
                var admitted = _enabled && IsMonitoredPathUnlocked(repoRoot, posixPath);
                if (!admitted)
                {
                    _logger?.Debug("[watch] delta rejected " + LogFields.Format(
                        LogFields.Field("repo", repoRoot),
                        LogFields.Field("path", posixPath),
                        LogFields.Field("reason", "not-in-inventory")));
                }

                return admitted;
            }
        }

        private void OnQueueChanged(object sender, ReviewQueue queue)
        {
            lock (_gate)
            {
                _queueIdleTimer?.Dispose();
                _queueIdleTimer = null;
                if (!ShouldPruneWhenIdle(queue))
                {
                    return;
                }

                _queueIdleTimer = new Timer(
                    _ =>
                    {
                        lock (_gate)
                        {
                            _queueIdleTimer?.Dispose();
                            _queueIdleTimer = null;
                            if (_disposed || !_enabled)
                            {
                                return;
                            }

                            var keep = PathsToKeepUnlocked();
                            _host?.PruneMonitor(_inventories.Keys.Select(ResolveRepoRoot).ToList(), keep);
                            _logger?.Debug("[watch] queue idle, monitor re-pruned " + LogFields.Format(
                                LogFields.Field("repos", _inventories.Count),
                                LogFields.Field("keepPaths", keep.Count)));
                        }
                    },
                    null,
                    QueueIdlePruneDelayMs,
                    Timeout.Infinite);
            }
        }

        private bool ShouldPruneWhenIdle(ReviewQueue queue)
        {
            if (_disposed || !_enabled)
            {
                return false;
            }

            if (queue == null || queue.Count > 0)
            {
                return false;
            }

            return _inventories.Count > 0;
        }

        private void PauseWatching()
        {
            lock (_gate)
            {
                CancelPendingRefreshesUnlocked();
                foreach (var normalizedRoot in _watched.Keys.ToList())
                {
                    var repoRoot = _knownRoots.TryGetValue(normalizedRoot, out var known) ? known : normalizedRoot;
                    _logger?.Info("[watch] stopped " + LogFields.Format(
                        LogFields.Field("repo", repoRoot),
                        LogFields.Field("reason", "automatic-analysis-stopped")));
                    _client.StopWatchFiles(repoRoot);
                }

                _watched.Clear();
                PublishActiveReposUnlocked();
            }

            _host?.PruneMonitor(Array.Empty<string>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private bool IsMonitoredPathUnlocked(string repoRoot, string relPath)
        {
            var key = RpcPath.NormalizeFsPath(repoRoot);
            if (_inventories.TryGetValue(key, out var inventory) && inventory.Contains(relPath))
            {
                return true;
            }

            return IsDirty(repoRoot, relPath);
        }

        private Task ScheduleInventoryRefreshUnlocked(string repoRoot, string key)
        {
            if (_pendingRefreshes.TryGetValue(key, out var existing))
            {
                existing.Timer.Change(InventoryRefreshDelayMs, Timeout.Infinite);
                return existing.Settled.Task;
            }

            var pending = new PendingRefresh();
            pending.Timer = new Timer(
                _ =>
                {
                    lock (_gate)
                    {
                        if (_pendingRefreshes.TryGetValue(key, out var current))
                        {
                            current.Timer.Dispose();
                            _pendingRefreshes.Remove(key);
                        }
                    }

                    _ = RefreshThenSettleAsync(repoRoot, pending);
                },
                null,
                InventoryRefreshDelayMs,
                Timeout.Infinite);
            _pendingRefreshes[key] = pending;
            return pending.Settled.Task;
        }

        private async Task RefreshThenSettleAsync(string repoRoot, PendingRefresh pending)
        {
            await RefreshInventoryAsync(repoRoot).ConfigureAwait(false);
            pending.Settled.TrySetResult(true);
        }

        private async Task RefreshInventoryAsync(string repoRoot)
        {
            _logger?.Debug("[watch] inventory refresh requested " + LogFields.Format(LogFields.Field("repo", repoRoot)));
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
                _logger?.Debug("[watch] inventory refresh skipped " + LogFields.Format(
                    LogFields.Field("repo", repoRoot),
                    LogFields.Field("error", ex.Message)));
            }
        }

        private void CancelPendingRefreshesUnlocked()
        {
            foreach (var pending in _pendingRefreshes.Values)
            {
                pending.Timer.Dispose();
                pending.Settled.TrySetResult(false);
            }

            _pendingRefreshes.Clear();
            _queueIdleTimer?.Dispose();
            _queueIdleTimer = null;
        }

        private sealed class PendingRefresh
        {
            public Timer Timer { get; set; }

            public TaskCompletionSource<bool> Settled { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
