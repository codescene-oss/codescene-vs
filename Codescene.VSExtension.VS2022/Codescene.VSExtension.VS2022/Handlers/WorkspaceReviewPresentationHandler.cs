// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Application.Cli.Rpc;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.VS2022.EditorMargin;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Handlers
{
    [Export(typeof(WorkspaceReviewPresentationHandler))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class WorkspaceReviewPresentationHandler : IDisposable
    {
        private readonly IWorkspaceReviewListener _listener;
        private readonly IErrorListWindowHandler _errorListWindowHandler;
        private readonly CodeSceneMarginSettingsManager _marginSettings;
        private readonly IAsyncTaskScheduler _scheduler;
        private readonly IAceRefactorService _aceRefactorService;
        private readonly IOpenDocumentContentProvider _openDocumentContentProvider;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public WorkspaceReviewPresentationHandler(
            IWorkspaceReviewListener listener,
            IErrorListWindowHandler errorListWindowHandler,
            CodeSceneMarginSettingsManager marginSettings,
            IAsyncTaskScheduler scheduler,
            IAceRefactorService aceRefactorService,
            ILogger logger,
            [Import(AllowDefault = true)] IOpenDocumentContentProvider openDocumentContentProvider = null)
        {
            _listener = listener;
            _errorListWindowHandler = errorListWindowHandler;
            _marginSettings = marginSettings;
            _scheduler = scheduler;
            _aceRefactorService = aceRefactorService;
            _logger = logger;
            _openDocumentContentProvider = openDocumentContentProvider;
            _listener.ReviewApplied += OnReviewApplied;
            _listener.ReviewFailed += OnReviewFailed;
        }

        public void Dispose()
        {
            _listener.ReviewApplied -= OnReviewApplied;
            _listener.ReviewFailed -= OnReviewFailed;
        }

        private void OnReviewApplied(object sender, FileReviewAppliedEventArgs e)
        {
            _scheduler.Schedule(async ct =>
            {
                try
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    _errorListWindowHandler.Handle(e.Review);
                    _marginSettings.NotifyScoreUpdated();

                    var content = await GetContentAsync(e.AbsolutePath);
                    if (content != null)
                    {
                        await _aceRefactorService.CheckContainsRefactorableFunctionsAsync(e.Review, content);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to present review for {e.AbsolutePath}", ex);
                }
            });
        }

        private void OnReviewFailed(object sender, ReviewFailedEventArgs e)
        {
            _logger.Debug($"Review failed for {e.AbsolutePath}: {e.Message}");
        }

        private async System.Threading.Tasks.Task<string> GetContentAsync(string path)
        {
            if (_openDocumentContentProvider == null)
            {
                return null;
            }

            try
            {
                return await _openDocumentContentProvider.GetContentForReviewAsync(path);
            }
            catch
            {
                return null;
            }
        }
    }
}
