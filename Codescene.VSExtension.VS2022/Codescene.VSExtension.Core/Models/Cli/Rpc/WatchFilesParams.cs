// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    internal class WatchFilesParams
    {
        [JsonProperty("repo-root")]
        public string RepoRoot { get; set; }

        [JsonProperty("relative-paths", NullValueHandling = NullValueHandling.Ignore)]
        public string[] RelativePaths { get; set; }
    }
}
