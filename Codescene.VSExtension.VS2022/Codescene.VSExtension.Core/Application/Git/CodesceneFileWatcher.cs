// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;

namespace Codescene.VSExtension.Core.Application.Git
{
    public sealed class CodesceneFileWatcher : IDisposable
    {
        public const string CodesceneDir = ".codescene";
        public const string CodeHealthRulesFileName = "code-health-rules.json";
        public const string ConfigFileName = "config.json";

        private readonly ILogger _logger;
        private readonly string _changeLogMessage;
        private FileSystemWatcher _watcher;
        private bool _disposed;

        public CodesceneFileWatcher(string gitRootPath, string fileName, ILogger logger, string changeLogMessage = null)
        {
            _logger = logger;
            _changeLogMessage = changeLogMessage;
            if (string.IsNullOrEmpty(gitRootPath) || !Directory.Exists(gitRootPath))
            {
                return;
            }

            var codescenePath = Path.Combine(gitRootPath, CodesceneDir);
            if (!Directory.Exists(codescenePath))
            {
                return;
            }

            try
            {
                _watcher = new FileSystemWatcher(codescenePath)
                {
                    Filter = fileName,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                };
                _watcher.Created += OnFileEvent;
                _watcher.Changed += OnFileEvent;
                _watcher.Deleted += OnFileEvent;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"CodesceneFileWatcher: Could not create watcher for {codescenePath}", ex);
            }
        }

        public event EventHandler FileChanged;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_watcher != null)
            {
                try
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Created -= OnFileEvent;
                    _watcher.Changed -= OnFileEvent;
                    _watcher.Deleted -= OnFileEvent;
                    _watcher.Dispose();
                }
                catch
                {
                }

                _watcher = null;
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private void OnFileEvent(object sender, FileSystemEventArgs e)
        {
            if (!string.IsNullOrEmpty(_changeLogMessage))
            {
                _logger?.Info(_changeLogMessage);
            }

            CacheGeneration.Increment();
            try
            {
                FileChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger?.Error("CodesceneFileWatcher: Error in FileChanged handler", ex);
            }
        }
    }
}
