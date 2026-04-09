// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class CodeHealthRulesWatcher : IDisposable
    {
        private const string RulesFileName = "code-health-rules.json";
        private const string CodesceneDir = ".codescene";

        private readonly ILogger _logger;
        private FileSystemWatcher _watcher;
        private bool _disposed;

        public CodeHealthRulesWatcher(string gitRootPath, ILogger logger)
        {
            _logger = logger;
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
                    Filter = RulesFileName,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                };
                _watcher.Created += OnRulesFileEvent;
                _watcher.Changed += OnRulesFileEvent;
                _watcher.Deleted += OnRulesFileEvent;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                _logger?.Warn($"CodeHealthRulesWatcher: Could not create watcher for {codescenePath}: {ex.Message}");
            }
        }

        public event EventHandler RulesFileChanged;

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
                    _watcher.Created -= OnRulesFileEvent;
                    _watcher.Changed -= OnRulesFileEvent;
                    _watcher.Deleted -= OnRulesFileEvent;
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

        private void OnRulesFileEvent(object sender, FileSystemEventArgs e)
        {
            _logger.Info("Code health rules change detected.");
            CacheGeneration.Increment();
            try
            {
                RulesFileChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger?.Warn($"CodeHealthRulesWatcher: Error in RulesFileChanged handler: {ex.Message}");
            }
        }
    }
}
