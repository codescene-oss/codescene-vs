// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly Func<FileChangeEvent, List<string>, Task> _processEventCallback;
        private readonly Func<Task<List<string>>> _getChangedFilesCallback;
        private readonly IAsyncTaskScheduler _taskScheduler;
        private Timer _scheduledTimer;

        public FileChangeEventProcessor(
            ILogger logger,
            IAsyncTaskScheduler taskScheduler,
            Func<FileChangeEvent, List<string>, Task> processEventCallback,
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

        public void Start(TimeSpan interval)
        {
            _scheduledTimer = new Timer(ProcessQueuedEventsCallback, null, interval, interval);
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
            var events = new List<FileChangeEvent>();

            while (_eventQueue.TryDequeue(out var evt))
            {
                events.Add(evt);
            }

            if (events.Count == 0)
            {
                return;
            }

            var coalesced = CoalesceByPath(events);

#if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeObserverCore: Processing {coalesced.Count} coalesced file change events (from {events.Count} raw)");
#endif

            var changedFiles = await _getChangedFilesCallback();

            foreach (var evt in coalesced)
            {
                var capturedEvt = evt;
                var capturedChangedFiles = changedFiles;
                _taskScheduler.Schedule(async () =>
                {
                    await _concurrencySemaphore.WaitAsync();
                    try
                    {
                        await _processEventCallback(capturedEvt, capturedChangedFiles);
                    }
                    catch (Exception ex)
                    {
                        _logger?.Warn($"GitChangeObserver: Error processing file change event: {ex.Message}");
                    }
                    finally
                    {
                        _concurrencySemaphore.Release();
                    }
                });
            }
        }
    }
}
