// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Delta;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;
using Codescene.VSExtension.VS2022.EditorMargin;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Application.Adapters
{
    [Export(typeof(IReviewPipelinePresentation))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class VisualStudioReviewPipelinePresentation : IReviewPipelinePresentation
    {
        private readonly IModelMapper _mapper;
        private readonly IErrorListWindowHandler _errorListWindowHandler;
        private readonly ICodeHealthMonitorNotifier _notifier;
        private readonly IAsyncTaskScheduler _scheduler;
        private readonly CodeSceneMarginSettingsManager _marginSettings;
        private readonly ITelemetryManager _telemetryManager;

        [ImportingConstructor]
        public VisualStudioReviewPipelinePresentation(
            IModelMapper mapper,
            IErrorListWindowHandler errorListWindowHandler,
            ICodeHealthMonitorNotifier notifier,
            IAsyncTaskScheduler scheduler,
            CodeSceneMarginSettingsManager marginSettings,
            ITelemetryManager telemetryManager)
        {
            _mapper = mapper;
            _errorListWindowHandler = errorListWindowHandler;
            _notifier = notifier;
            _scheduler = scheduler;
            _marginSettings = marginSettings;
            _telemetryManager = telemetryManager;
        }

        public void ReviewStarted(ReviewDocument document)
        {
        }

        public void ReviewFinished(ReviewDocument document)
        {
        }

        public void DeltaStarted(ReviewDocument document)
        {
        }

        public void DeltaFinished(ReviewDocument document)
        {
        }

        public void PresentReview(PresentedReview review)
        {
            if (review?.Document == null || review.Result == null)
            {
                return;
            }

            var mapped = _mapper.Map(review.Document.FilePath, review.Result);
            if (mapped == null)
            {
                return;
            }

            new ReviewCacheService().Put(new ReviewCacheEntry(review.Content ?? string.Empty, review.Document.FilePath, mapped));
            if (!review.UpdateDiagnosticsPane)
            {
                return;
            }

            _scheduler.Schedule(async ct =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _errorListWindowHandler.Handle(mapped);
                _marginSettings.NotifyScoreUpdated();
            });
        }

        public void PresentDelta(PresentedDelta delta)
        {
            if (delta?.Document == null || !delta.UpdateMonitor)
            {
                return;
            }

            var cache = new DeltaCacheService();
            var snapshot = cache.GetAll();
            cache.SetPresentationDelta(delta.Document.FilePath, delta.Result);
            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(
                snapshot,
                cache.GetAll(),
                new DeltaCacheEntry(delta.Document.FilePath, string.Empty, string.Empty, delta.Result),
                _telemetryManager);
            _notifier.RequestViewUpdate();
            _marginSettings.NotifyScoreUpdated();
        }

        public void Remove(ReviewDocument document)
        {
            if (document == null || string.IsNullOrEmpty(document.FilePath))
            {
                return;
            }

            new ReviewCacheService().Invalidate(document.FilePath);
            new DeltaCacheService().Invalidate(document.FilePath);
            _scheduler.Schedule(async ct =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _errorListWindowHandler.Handle(new FileReviewModel
                {
                    FilePath = document.FilePath,
                    FileLevel = new List<CodeSmellModel>(),
                    FunctionLevel = new List<CodeSmellModel>(),
                });
                _marginSettings.NotifyScoreUpdated();
            });
            _notifier.RequestViewUpdate();
        }

        public void Failed(Exception error)
        {
            _notifier.RequestViewUpdate();
        }
    }
}
