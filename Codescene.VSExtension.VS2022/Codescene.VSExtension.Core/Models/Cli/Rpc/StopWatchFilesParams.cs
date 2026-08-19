// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class StopWatchFilesParams
    {
        [JsonProperty("repo-root")]
        public string RepoRoot { get; set; }
    }
}
