// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class DeltaNotification
    {
        public string Id { get; set; }

        public string Path { get; set; }

        public string RepoRoot { get; set; }

        public DeltaResponseModel Result { get; set; }

        public ReviewQueue Queue { get; set; }
    }
}
