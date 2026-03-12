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
        private readonly Func<FileChangeEvent, List<string>, long?, CancellationToken, Task> _processEventCallback;
        private readonly Func<Task<List<string>>> _getChangedFilesCallback;
        private readonly IAsyncTaskScheduler _taskScheduler;
        private Timer _scheduledTimer;
        private CancellationToken _cancellationToken;

        public FileChangeEventProcessor(
            ILogger logger,
            IAsyncTaskScheduler taskScheduler,
            Func<FileChangeEvent, List<string>, long?, CancellationToken, Task> processEventCallback,
            Func<Task<List<string>>> getChangedFilesCallback)
        {
            _logger = logger;
            _taskScheduler = taskScheduler;
            _processEventCallback = processEventCallback;
            _getChangedFilesCallback = getChangedFilesCallback;

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

        /// <summary>
        /// Merges multiple events for the same path into one event per path.
        /// It selects the last event in the queue per file.
        ///
        /// It will return either Delete or Change (Create is converted) per file.
        /// </summary>
        private static List<FileChangeEvent> CoalesceByPath(List<FileChangeEvent> events)
        {
            var byPath = new Dictionary<string, FileChangeType>(StringComparer.OrdinalIgnoreCase);

            foreach (var evt in events)
            {
                var path = evt.FilePath;
                byPath[path] = evt.Type == FileChangeType.Delete
                    ? FileChangeType.Delete
                    : FileChangeType.Change;
            }

            var result = new List<FileChangeEvent>(byPath.Count);
            foreach (var kv in byPath)
            {
                result.Add(new FileChangeEvent(kv.Value, kv.Key));
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
                    _logger?.Warn($"GitChangeObserver: Error processing queued events: {ex.Message}");
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

            var changedFiles = await _getChangedFilesCallback();

            if (_cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ScheduleCoalescedEvents(coalesced, changedFiles, operationGeneration);
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

        private void ScheduleCoalescedEvents(List<FileChangeEvent> coalesced, List<string> changedFiles, long? operationGeneration)
        {
            var token = _cancellationToken;
            foreach (var evt in coalesced)
            {
                var capturedEvt = evt;
                var capturedChangedFiles = changedFiles;
                _taskScheduler.Schedule(() => ProcessOneEventAsync(capturedEvt, capturedChangedFiles, operationGeneration, token));
            }
        }

        private async Task ProcessOneEventAsync(FileChangeEvent evt, List<string> changedFiles, long? operationGeneration, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            await _concurrencySemaphore.WaitAsync(token);
            try
            {
                await _processEventCallback(evt, changedFiles, operationGeneration, token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger?.Warn($"GitChangeObserver: Error processing file change event: {ex.Message}");
            }
            finally
            {
                _concurrencySemaphore.Release();
            }
        }
    }
}
