// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.VS2022.Application.Git
{
    [Export(typeof(ISavedFilesTracker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class SavedFilesTracker : ISavedFilesTracker, IDisposable
    {
        private readonly object _initLock = new object();

        [Import]
        private IDocumentSaveEventSource _eventSource;

        [Import]
        private IOpenFilesObserver _openFilesObserver;

        [Import]
        private ILogger _logger;

        private SavedFilesTrackerCore _core;
        private bool _disposed;

        public IEnumerable<string> GetSavedFiles()
        {
            EnsureInitialized();
            return _core.GetSavedFiles();
        }

        public void ClearSavedFiles()
        {
            EnsureInitialized();
            _core.ClearSavedFiles();
        }

        public void RemoveFromTracker(string filePath)
        {
            EnsureInitialized();
            _core.RemoveFromTracker(filePath);
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
                _core?.Dispose();
            }

            _disposed = true;
        }

        private void EnsureInitialized()
        {
            if (_core != null)
            {
                return;
            }

            lock (_initLock)
            {
                if (_core != null)
                {
                    return;
                }

                _core = new SavedFilesTrackerCore(_eventSource, _openFilesObserver, _logger);
                _core.Start();
            }
        }
    }
}
