// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Delta;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class DeltaReviewNotification
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("repo-root")]
        public string RepoRoot { get; set; }

        [JsonProperty("result")]
        public DeltaResponseModel Result { get; set; }

        [JsonProperty("queue")]
        public ReviewQueueModel Queue { get; set; }
    }
}
