// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Models.Cli.Rpc;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IReviewPipelinePresentation
    {
        void ReviewStarted(ReviewDocument document);

        void ReviewFinished(ReviewDocument document);

        void DeltaStarted(ReviewDocument document);

        void DeltaFinished(ReviewDocument document);

        void PresentReview(PresentedReview review);

        void PresentDelta(PresentedDelta delta);

        void Remove(ReviewDocument document);

        void Failed(Exception error);
    }
}
