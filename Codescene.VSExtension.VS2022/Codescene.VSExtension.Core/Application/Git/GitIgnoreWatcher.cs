// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using Codescene.VSExtension.Core.Interfaces;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class GitIgnoreWatcher : IDisposable
    {
        private readonly ILogger _logger;
        private FileSystemWatcher _watcher;
        private bool _disposed;

        public GitIgnoreWatcher(string gitRootPath, ILogger logger)
        {
            _logger = logger;
            if (string.IsNullOrEmpty(gitRootPath) || !Directory.Exists(gitRootPath))
            {
                return;
            }

            try
            {
                _watcher = new FileSystemWatcher(gitRootPath)
                {
                    Filter = ".gitignore",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                    IncludeSubdirectories = true,
                };
                _watcher.Created += OnGitIgnoreEvent;
                _watcher.Changed += OnGitIgnoreEvent;
                _watcher.Deleted += OnGitIgnoreEvent;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"GitIgnoreWatcher: Could not create watcher for {gitRootPath}", ex);
            }
        }

        public event EventHandler GitIgnoreChanged;

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
                    _watcher.Created -= OnGitIgnoreEvent;
                    _watcher.Changed -= OnGitIgnoreEvent;
                    _watcher.Deleted -= OnGitIgnoreEvent;
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

        private void OnGitIgnoreEvent(object sender, FileSystemEventArgs e)
        {
            try
            {
                GitIgnoreChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger?.Error("GitIgnoreWatcher: Error in GitIgnoreChanged handler", ex);
            }
        }
    }
}
