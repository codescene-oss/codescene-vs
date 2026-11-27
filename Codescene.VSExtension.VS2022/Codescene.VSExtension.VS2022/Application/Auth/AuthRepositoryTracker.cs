// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.Core.Util;
using Codescene.VSExtension.VS2022.Application.Git;
using Community.VisualStudio.Toolkit;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Application.Auth
{
    [Export]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class AuthRepositoryTracker : IDisposable
    {
        private readonly IAuthService _service;
        private readonly IAsyncTaskScheduler _scheduler;
        private readonly object _gate = new object();
        private EnvDTE.WindowEvents _windowEvents;
        private bool _active;
        private int _generation;
        private bool _disposed;

        [ImportingConstructor]
        public AuthRepositoryTracker(IAuthService service, IAsyncTaskScheduler scheduler)
        {
            _service = service;
            _scheduler = scheduler;
        }

        public void Start()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _service.SnapshotChanged += OnSnapshotChanged;

            var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE;
            if (dte != null)
            {
                _windowEvents = dte.Events.WindowEvents;
                _windowEvents.WindowActivated += OnWindowActivated;
            }

            VS.Events.SolutionEvents.OnAfterOpenSolution += OnSolutionOpened;
            VS.Events.SolutionEvents.OnAfterCloseSolution += OnSolutionClosed;
            VS.Events.SolutionEvents.OnAfterOpenFolder += OnFolderOpened;
            VS.Events.SolutionEvents.OnAfterCloseFolder += OnFolderClosed;
            VS.Events.SolutionEvents.OnAfterLoadProject += OnProjectChanged;

            if (_service.Current.Active)
            {
                EvaluateRepository();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _service.SnapshotChanged -= OnSnapshotChanged;

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (_windowEvents != null)
                {
                    _windowEvents.WindowActivated -= OnWindowActivated;
                    _windowEvents = null;
                }
            });

            VS.Events.SolutionEvents.OnAfterOpenSolution -= OnSolutionOpened;
            VS.Events.SolutionEvents.OnAfterCloseSolution -= OnSolutionClosed;
            VS.Events.SolutionEvents.OnAfterOpenFolder -= OnFolderOpened;
            VS.Events.SolutionEvents.OnAfterCloseFolder -= OnFolderClosed;
            VS.Events.SolutionEvents.OnAfterLoadProject -= OnProjectChanged;
        }

        private void OnSnapshotChanged(object sender, AuthSnapshot snapshot)
        {
            var wasActive = _active;
            _active = snapshot.Active;

            if (!wasActive && _active)
            {
#pragma warning disable VSTHRD010
                EvaluateRepository();
#pragma warning restore VSTHRD010
            }
        }

        private void OnWindowActivated(EnvDTE.Window gotFocus, EnvDTE.Window lostFocus)
        {
            if (!_active)
            {
                return;
            }

            if (gotFocus?.Document == null)
            {
                return;
            }

#pragma warning disable VSTHRD010
            EvaluateRepository();
#pragma warning restore VSTHRD010
        }

        private void OnSolutionOpened(Community.VisualStudio.Toolkit.Solution solution)
        {
            if (_active)
            {
#pragma warning disable VSTHRD010
                EvaluateRepository();
#pragma warning restore VSTHRD010
            }
        }

        private void OnSolutionClosed()
        {
            if (!_active)
            {
                return;
            }

            _scheduler.Schedule(async ct =>
            {
                await _service.SetRepoAsync(null);
            });
        }

        private void OnFolderOpened(string folder)
        {
            if (_active)
            {
#pragma warning disable VSTHRD010
                EvaluateRepository();
#pragma warning restore VSTHRD010
            }
        }

        private void OnFolderClosed(string folder)
        {
            if (_active)
            {
#pragma warning disable VSTHRD010
                EvaluateRepository();
#pragma warning restore VSTHRD010
            }
        }

        private void OnProjectChanged(SolutionItem project)
        {
            if (_active)
            {
#pragma warning disable VSTHRD010
                EvaluateRepository();
#pragma warning restore VSTHRD010
            }
        }

        private void EvaluateRepository()
        {
            int generation;
            lock (_gate)
            {
                generation = ++_generation;
            }

            _scheduler.Schedule(async ct =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var solution = await VS.Solutions.GetCurrentSolutionAsync();
                var solutionPath = solution?.FullPath;
                var directories = SolutionProjectDiscovery.GetProjectDirectories(
                    await VS.Services.GetSolutionAsync(),
                    solutionPath);

                string activeFile = null;
                var activeDocument = await VS.Documents.GetActiveDocumentViewAsync();
                if (activeDocument?.Document?.FilePath != null)
                {
                    activeFile = activeDocument.Document.FilePath;
                }

                var directoryList = directories.ToList();
                var repo = await System.Threading.Tasks.Task.Run(() => AuthRepoPicker.Pick(GitPathDiscovery.ResolveRepositoryRoots(directoryList), activeFile));

                lock (_gate)
                {
                    if (generation != _generation)
                    {
                        return;
                    }
                }

                await _service.SetRepoAsync(repo);
            });
        }
    }
}
