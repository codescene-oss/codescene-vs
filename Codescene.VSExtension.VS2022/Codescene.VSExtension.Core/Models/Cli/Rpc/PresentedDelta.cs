// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class PresentedDelta : ReviewSubmission
    {
        public DeltaResponseModel Result { get; set; }
    }
}
