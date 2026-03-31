// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces.Git;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codescene.VSExtension.VS2022.Application.Adapters
{
    [Export(typeof(IDocumentSaveEventSource))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class VsDocumentSaveEventSource : IDocumentSaveEventSource, IVsRunningDocTableEvents
    {
        private IVsRunningDocumentTable _runningDocumentTable;
        private uint _cookie;
        private bool _disposed;
        private bool _started;

        public event EventHandler<string> DocumentSaved;

        public void Start()
        {
            if (_started)
            {
                return;
            }

            ThreadHelper.ThrowIfNotOnUIThread();

            _runningDocumentTable = ServiceProvider.GlobalProvider.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
            if (_runningDocumentTable != null)
            {
                _runningDocumentTable.AdviseRunningDocTableEvents(this, out _cookie);
            }

            _started = true;
        }

        public int OnAfterSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var filePath = GetDocumentPath(docCookie);
            if (!string.IsNullOrEmpty(filePath))
            {
                DocumentSaved?.Invoke(this, filePath);
            }

            return VSConstants.S_OK;
        }

        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
        }

        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame)
        {
            return VSConstants.S_OK;
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
                if (_runningDocumentTable != null && _cookie != 0)
                {
                    ThreadHelper.JoinableTaskFactory.Run(async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        _runningDocumentTable.UnadviseRunningDocTableEvents(_cookie);
                    });
                }
            }

            _disposed = true;
        }

        private string GetDocumentPath(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (_runningDocumentTable == null)
            {
                return null;
            }

            _runningDocumentTable.GetDocumentInfo(
                docCookie,
                out _,
                out _,
                out _,
                out var moniker,
                out _,
                out _,
                out var docData);

            if (docData != IntPtr.Zero)
            {
                System.Runtime.InteropServices.Marshal.Release(docData);
            }

            return moniker;
        }
    }
}
