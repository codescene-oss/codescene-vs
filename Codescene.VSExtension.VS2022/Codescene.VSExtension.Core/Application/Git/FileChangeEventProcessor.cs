// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Enums.Git;
using Codescene.VSExtension.Core.Interfaces;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class FileChangeEventProcessor : IDisposable
    {
        private const int MaxConcurrentFileProcessing = 4;

        private readonly ConcurrentQueue<FileChangeEvent> _eventQueue = new ConcurrentQueue<FileChangeEvent>();
        private readonly SemaphoreSlim _concurrencySemaphore = new SemaphoreSlim(MaxConcurrentFileProcessing, MaxConcurrentFileProcessing);
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

            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> GitChangeObserverCore: Processing {events.Count} queued file change events");
            #endif

            var changedFiles = await _getChangedFilesCallback();

            foreach (var evt in events)
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
