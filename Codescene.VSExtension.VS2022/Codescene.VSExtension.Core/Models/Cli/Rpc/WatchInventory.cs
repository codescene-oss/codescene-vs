// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class WatchInventory
    {
        public string RepoRoot { get; set; }

        public IReadOnlyList<string> Files { get; set; } = new List<string>();
    }
}
