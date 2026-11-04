// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public abstract class ResultNotification<TResult>
    {
        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("repo-root")]
        public string RepoRoot { get; set; }

        [JsonProperty("result")]
        public TResult Result { get; set; }
    }
}
