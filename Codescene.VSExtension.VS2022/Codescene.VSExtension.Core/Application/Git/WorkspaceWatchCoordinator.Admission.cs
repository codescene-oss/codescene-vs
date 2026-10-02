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
            if (AlreadyAdmitted(reportedRoot, relPath, out repoRoot, out posixPath, out key))
            {
                return true;
            }

            if (!TryScheduleRefresh(repoRoot, posixPath, key, out var refresh))
            {
                return true;
            }

            await refresh.ConfigureAwait(false);
            if (AdmittedAfterRefresh(repoRoot, posixPath))
            {
                return true;
            }

            _logger?.Debug("[watch] delta rejected " + LogFields.Format(
                LogFields.Field("repo", repoRoot),
                LogFields.Field("path", posixPath),
                LogFields.Field("reason", "not-in-inventory")));
            return false;
        }

        private bool AlreadyAdmitted(string reportedRoot, string relPath, out string repoRoot, out string posixPath, out string key)
        {
            var missingInventory = false;
            lock (_gate)
            {
                repoRoot = ResolveRepoRoot(reportedRoot);
                posixPath = RpcPath.ToPosixRelPath(relPath);
                key = RpcPath.NormalizeFsPath(repoRoot);
                if (!_inventories.ContainsKey(key))
                {
                    missingInventory = true;
                }
                else if (InventoryContainsUnlocked(repoRoot, posixPath))
                {
                    return true;
                }
            }

            if (missingInventory)
            {
                _logger?.Debug("[watch] delta admitted " + LogFields.Format(
                    LogFields.Field("repo", repoRoot),
                    LogFields.Field("path", posixPath),
                    LogFields.Field("reason", "no-inventory")));
                return true;
            }

            return IsDirty(repoRoot, posixPath);
        }

        private bool TryScheduleRefresh(string repoRoot, string posixPath, string key, out Task refresh)
        {
            lock (_gate)
            {
                if (InventoryContainsUnlocked(repoRoot, posixPath))
                {
                    refresh = Task.CompletedTask;
                    return false;
                }

                _logger?.Debug("[watch] delta reconcile scheduled " + LogFields.Format(
                    LogFields.Field("repo", repoRoot),
                    LogFields.Field("path", posixPath),
                    LogFields.Field("reason", "not-in-inventory")));
                refresh = ScheduleInventoryRefreshUnlocked(repoRoot, key);
                return true;
            }
        }

        private bool AdmittedAfterRefresh(string repoRoot, string posixPath)
        {
            var inInventory = false;
            lock (_gate)
            {
                inInventory = _enabled && InventoryContainsUnlocked(repoRoot, posixPath);
            }

            return inInventory || IsDirty(repoRoot, posixPath);
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
                        var prune = false;
                        lock (_gate)
                        {
                            _queueIdleTimer?.Dispose();
                            _queueIdleTimer = null;
                            prune = !_disposed && _enabled;
                        }

                        if (prune)
                        {
                            PruneMonitor();
                            _logger?.Debug("[watch] queue idle, monitor re-pruned");
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

        private bool InventoryContainsUnlocked(string repoRoot, string relPath)
        {
            var key = RpcPath.NormalizeFsPath(repoRoot);
            return _inventories.TryGetValue(key, out var inventory) && inventory.Contains(relPath);
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
