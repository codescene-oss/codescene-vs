// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    internal class CheckRulesRequestParams
    {
        [JsonProperty("repo-root")]
        public string RepoRoot { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }
    }
}
