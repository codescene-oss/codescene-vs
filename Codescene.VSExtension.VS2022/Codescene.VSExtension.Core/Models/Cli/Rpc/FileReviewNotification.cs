// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Review;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class FileReviewNotification
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("repo-root")]
        public string RepoRoot { get; set; }

        [JsonProperty("result")]
        public CliReviewModel Result { get; set; }

        [JsonProperty("queue")]
        public ReviewQueueModel Queue { get; set; }
    }
}
