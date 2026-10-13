// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models.Cli.Rpc;

namespace Codescene.VSExtension.Core.Tests
{
    internal sealed class ReviewPipelinePresentationEvents : IReviewPipelinePresentation
    {
        public List<PresentedReview> Reviews { get; } = new List<PresentedReview>();

        public List<PresentedDelta> Deltas { get; } = new List<PresentedDelta>();

        public List<ReviewDocument> Removed { get; } = new List<ReviewDocument>();

        public List<Exception> Failures { get; } = new List<Exception>();

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
            Reviews.Add(review);
        }

        public void PresentDelta(PresentedDelta delta)
        {
            Deltas.Add(delta);
        }

        public void Remove(ReviewDocument document)
        {
            Removed.Add(document);
        }

        public void Failed(Exception error)
        {
            Failures.Add(error);
        }
    }
}
