// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class PresentedReview : ReviewSubmission
    {
        public CliReviewModel Result { get; set; }
    }
}
