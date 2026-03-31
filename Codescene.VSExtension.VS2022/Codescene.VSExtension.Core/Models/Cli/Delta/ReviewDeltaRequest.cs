// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Cli.Delta
{
    public class ReviewDeltaRequest
    {
        public string OldScore { get; set; }

        public string NewScore { get; set; }

        public string FilePath { get; set; }

        public string FileContent { get; set; }
    }
}
