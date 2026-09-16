// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class ReviewFailedNotification
    {
        public string Id { get; set; }

        public string Path { get; set; }

        public string RepoRoot { get; set; }

        public string Message { get; set; }

        public ReviewQueue Queue { get; set; }
    }
}
