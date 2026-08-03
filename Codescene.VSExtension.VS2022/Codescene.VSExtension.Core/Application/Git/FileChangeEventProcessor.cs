// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Enums.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class FileChangeEventProcessor : IDisposable
    {
        private readonly ConcurrentQueue<FileChangeEvent> _eventQueue = new ConcurrentQueue<FileChangeEvent>();
        private readonly SemaphoreSlim _concurrencySemaphore;
        private readonly ILogger _logger;
        private readonly Func<FileChangeEvent, List<string>, long?, CancellationToken, string, Task> _processEventCallback;
        private readonly Func<string, Task<List<string>>> _getChangedFilesCallback;
        private readonly Func<bool> _shouldSkipNonDeletesCallback;
        private readonly Func<string> _getBaselineCommitCallback;
        private readonly IAsyncTaskScheduler _taskScheduler;
        private Timer _scheduledTimer;
        private CancellationToken _cancellationToken;

        public FileChangeEventProcessor(
            ILogger logger,
            IAsyncTaskScheduler taskScheduler,
            Func<FileChangeEvent, List<string>, long?, CancellationToken, string, Task> processEventCallback,
            Func<string, Task<List<string>>> getChangedFilesCallback,
            Func<bool> shouldSkipNonDeletesCallback = null,
            Func<string> getBaselineCommitCallback = null)
        {
            _logger = logger;
            _taskScheduler = taskScheduler;
            _processEventCallback = processEventCallback;
            _getChangedFilesCallback = getChangedFilesCallback;
            _shouldSkipNonDeletesCallback = shouldSkipNonDeletesCallback;
            _getBaselineCommitCallback = getBaselineCommitCallback;

            var numberOfThreads = CoreCountUtils.GetParallelizationCountByCoreCount(Environment.ProcessorCount);
            _concurrencySemaphore = new SemaphoreSlim(numberOfThreads, numberOfThreads);
        }

        public ConcurrentQueue<FileChangeEvent> EventQueue => _eventQueue;

        public Timer ScheduledTimer => _scheduledTimer;

        public void EnqueueEvent(FileChangeEvent evt)
        {
            _eventQueue.Enqueue(evt);
        }

        public void Start(TimeSpan interval, CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _scheduledTimer = new Timer(ProcessQueuedEventsCallback, null, interval, interval);
        }

        public void DrainAndStop()
        {
            _scheduledTimer?.Dispose();
            _scheduledTimer = null;
            while (_eventQueue.TryDequeue(out _))
            {
            }
        }

        public void Dispose()
        {
            _scheduledTimer?.Dispose();
            _scheduledTimer = null;
            _concurrencySemaphore?.Dispose();
        }

        private static bool HasDeleteEvents(List<FileChangeEvent> events)
        {
            foreach (var evt in events)
            {
                if (evt.Type == FileChangeType.Delete)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<FileChangeEvent> CoalesceByPath(List<FileChangeEvent> events)
        {
            var byPath = new Dictionary<string, FileChangeType>(StringComparer.OrdinalIgnoreCase);
            var hadDelete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var evt in events)
            {
                var path = evt.FilePath;
                if (evt.Type == FileChangeType.Delete)
                {
                    hadDelete.Add(path);
                }

                byPath[path] = evt.Type == FileChangeType.Delete
                    ? FileChangeType.Delete
                    : FileChangeType.Change;
            }

            var result = new List<FileChangeEvent>(byPath.Count);
            foreach (var kv in byPath)
            {
                var type = hadDelete.Contains(kv.Key) ? FileChangeType.Delete : kv.Value;
                result.Add(new FileChangeEvent(type, kv.Key));
            }

            return result;
        }

        private void ProcessQueuedEventsCallback(object state)
        {
            _taskScheduler.Schedule(async () =>
            {
                try
                {
                    await ProcessQueuedEventsAsync();
                }
                catch (Exception ex)
                {
                    _logger?.Error("GitChangeObserver: Error processing queued events", ex);
                }
            });
        }

        private async Task ProcessQueuedEventsAsync()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var operationGeneration = CacheGeneration.Current;

            var events = DrainQueue();

            if (events.Count == 0)
            {
                return;
            }

            var coalesced = CoalesceByPath(events);

#if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeObserverCore: Processing {coalesced.Count} coalesced file change events (from {events.Count} raw)");
#endif

            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var shouldSkipNonDeletes = _shouldSkipNonDeletesCallback?.Invoke() ?? false;

            if (shouldSkipNonDeletes && !HasDeleteEvents(coalesced))
            {
                _logger?.Debug("FileChangeEventProcessor: Skipping all events - current branch matches default branch and no deletes pending");
                return;
            }

            var baselineCommit = _getBaselineCommitCallback?.Invoke() ?? string.Empty;
            var changedFiles = await _getChangedFilesCallback(baselineCommit);

            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ScheduleCoalescedEvents(coalesced, changedFiles, operationGeneration, shouldSkipNonDeletes, baselineCommit);
        }

        private List<FileChangeEvent> DrainQueue()
        {
            var events = new List<FileChangeEvent>();
            while (_eventQueue.TryDequeue(out var evt))
            {
                events.Add(evt);
            }

            return events;
        }

        private void ScheduleCoalescedEvents(List<FileChangeEvent> coalesced, List<string> changedFiles, long? operationGeneration, bool shouldSkipNonDeletes, string baselineCommit)
        {
            var token = _cancellationToken;
            foreach (var evt in coalesced)
            {
                if (shouldSkipNonDeletes && evt.Type != FileChangeType.Delete)
                {
                    _logger?.Debug($"FileChangeEventProcessor: Skipping {evt.Type} event for {evt.FilePath} - current branch matches default branch");
                    continue;
                }

                var capturedEvt = evt;
                var capturedChangedFiles = changedFiles;
                _taskScheduler.Schedule(() => ProcessOneEventAsync(capturedEvt, capturedChangedFiles, operationGeneration, token, baselineCommit));
            }
        }

        private async Task ProcessOneEventAsync(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken token, string baselineCommit)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            await _concurrencySemaphore.WaitAsync(token);
            try
            {
                await _processEventCallback(evt, changedFiles, operationGeneration, token, baselineCommit);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger?.Error("GitChangeObserver: Error processing file change event", ex);
            }
            finally
            {
                _concurrencySemaphore.Release();
            }
        }
    }
}
