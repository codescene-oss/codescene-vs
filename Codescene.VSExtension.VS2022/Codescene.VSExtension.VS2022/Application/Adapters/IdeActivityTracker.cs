// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Application.Adapters
{
    [Export(typeof(IIdeActivityTracker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class IdeActivityTracker : IIdeActivityTracker
    {
        private bool? _testOverride;
        private volatile bool _isWindowActive = true;
        private bool _initialized;

        public IdeActivityTracker()
        {
            InitializeOnUIThread();
        }

        public bool IsIdeWindowActive()
        {
            if (_testOverride.HasValue)
            {
                return _testOverride.Value;
            }

            return _isWindowActive;
        }

        public void SetActiveForTesting(bool active)
        {
            _testOverride = active;
        }

        private void InitializeOnUIThread()
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (_initialized)
                {
                    return;
                }

                var mainWindow = System.Windows.Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    _isWindowActive = mainWindow.IsActive;
                    mainWindow.Activated += OnWindowActivated;
                    mainWindow.Deactivated += OnWindowDeactivated;
                    _initialized = true;
                }
            }).FileAndForget("IdeActivityTracker/Initialize");
        }

        private void OnWindowActivated(object sender, System.EventArgs e)
        {
            _isWindowActive = true;
        }

        private void OnWindowDeactivated(object sender, System.EventArgs e)
        {
            _isWindowActive = false;
        }
    }
}
