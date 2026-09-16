// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
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

        [ImportingConstructor]
        public VisualStudioReviewPipelinePresentation(
            IModelMapper mapper,
            IErrorListWindowHandler errorListWindowHandler,
            ICodeHealthMonitorNotifier notifier,
            IAsyncTaskScheduler scheduler,
            CodeSceneMarginSettingsManager marginSettings)
        {
            _mapper = mapper;
            _errorListWindowHandler = errorListWindowHandler;
            _notifier = notifier;
            _scheduler = scheduler;
            _marginSettings = marginSettings;
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

            new DeltaCacheService().SetPresentationDelta(delta.Document.FilePath, delta.Result);
            _notifier.RequestViewUpdate();
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
