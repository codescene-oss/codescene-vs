// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.Core.Util;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

namespace Codescene.VSExtension.VS2022.Application.Auth
{
    [Export]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class AuthFeatureBinder
    {
        private readonly IAuthService _service;
        private readonly IAsyncTaskScheduler _scheduler;
        private readonly AuthRepositoryTracker _repositoryTracker;
        private readonly SignInNudgeService _nudgeService;

        [ImportingConstructor]
        public AuthFeatureBinder(
            IAuthService service,
            IAsyncTaskScheduler scheduler,
            AuthRepositoryTracker repositoryTracker,
            SignInNudgeService nudgeService)
        {
            _service = service;
            _scheduler = scheduler;
            _repositoryTracker = repositoryTracker;
            _nudgeService = nudgeService;

            WebComponentJson.AuthSnapshotSource = () => _service.Current;

            _service.SnapshotChanged += OnSnapshotChanged;

            _service.Start();

            _scheduler.Schedule(async ct =>
            {
                await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _repositoryTracker.Start();
            });
        }

        private void OnSnapshotChanged(object sender, AuthSnapshot snapshot)
        {
#pragma warning disable VSTHRD010
            _nudgeService.OnSnapshotChanged(snapshot);
#pragma warning restore VSTHRD010

            if (!CodeSceneToolWindow.IsControlCreated)
            {
                return;
            }

            _scheduler.Schedule(ct => CodeSceneToolWindow.UpdateViewAsync());
        }
    }
}
