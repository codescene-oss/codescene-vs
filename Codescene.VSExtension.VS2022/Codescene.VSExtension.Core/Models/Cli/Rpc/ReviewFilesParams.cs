// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    internal class ReviewFilesParams
    {
        [JsonProperty("repo-root")]
        public string RepoRoot { get; set; }

        [JsonProperty("files")]
        public IList<ReviewFileParams> Files { get; set; }
    }
}
