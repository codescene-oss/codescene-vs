// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.Core.Application.Git
{
    public class SavedFilesTrackerCore : ISavedFilesTracker, IDisposable
    {
        private readonly HashSet<string> _savedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new object();
        private readonly IOpenFilesObserver _openFilesObserver;
        private readonly ILogger _logger;
        private readonly IDocumentSaveEventSource _eventSource;
        private bool _disposed;

        public SavedFilesTrackerCore(
            IDocumentSaveEventSource eventSource,
            IOpenFilesObserver openFilesObserver,
            ILogger logger)
        {
            _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
            _openFilesObserver = openFilesObserver ?? throw new ArgumentNullException(nameof(openFilesObserver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _eventSource.DocumentSaved += OnDocumentSaved;
        }

        public IEnumerable<string> GetSavedFiles()
        {
            lock (_lock)
            {
                return _savedFiles.ToList();
            }
        }

        public void ClearSavedFiles()
        {
            lock (_lock)
            {
                _savedFiles.Clear();
            }
        }

        public void RemoveFromTracker(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            lock (_lock)
            {
                _savedFiles.Remove(filePath);
            }
        }

        public void Start()
        {
            _eventSource.Start();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _eventSource.DocumentSaved -= OnDocumentSaved;
                _eventSource.Dispose();
            }

            _disposed = true;
        }

        private void OnDocumentSaved(object sender, string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            var visibleFiles = _openFilesObserver.GetAllVisibleFileNames();
            if (visibleFiles == null)
            {
                return;
            }

            var isVisible = visibleFiles.Any(f =>
                string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));

            if (!isVisible)
            {
                _logger.Debug($"SavedFilesTracker: Ignoring save for non-visible file: {filePath}");
                return;
            }

            lock (_lock)
            {
                _savedFiles.Add(filePath);
            }

            _logger.Debug($"SavedFilesTracker: Tracked save for: {filePath}");
        }
    }
}
