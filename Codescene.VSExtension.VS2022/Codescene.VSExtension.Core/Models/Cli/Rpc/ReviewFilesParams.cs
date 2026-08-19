// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class ReviewFilesParams
    {
        [JsonProperty("repo-root")]
        public string RepoRoot { get; set; }

        [JsonProperty("baseline-revision", NullValueHandling = NullValueHandling.Ignore)]
        public string BaselineRevision { get; set; }

        [JsonProperty("files")]
        public IList<ReviewFileItem> Files { get; set; }
    }
}
