// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class ReviewDocument
    {
        public string FilePath { get; set; }

        public string Content { get; set; }

        public bool IsDirty { get; set; }
    }
}
