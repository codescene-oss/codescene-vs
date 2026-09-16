// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class ReviewNotification
    {
        public string Id { get; set; }

        public string Path { get; set; }

        public string RepoRoot { get; set; }

        public CliReviewModel Result { get; set; }

        public ReviewQueue Queue { get; set; }
    }
}
