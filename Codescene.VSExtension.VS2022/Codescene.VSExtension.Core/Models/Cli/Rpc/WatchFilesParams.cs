// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class WatchFilesParams
    {
        [JsonProperty("repo-root")]
        public string RepoRoot { get; set; }

        [JsonProperty("baseline-revision", NullValueHandling = NullValueHandling.Ignore)]
        public string BaselineRevision { get; set; }
    }
}
