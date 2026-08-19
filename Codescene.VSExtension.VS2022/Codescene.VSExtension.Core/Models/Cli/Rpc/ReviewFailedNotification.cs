// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class ReviewFailedNotification
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; }

        [JsonProperty("repo-root")]
        public string RepoRoot { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("queue")]
        public ReviewQueueModel Queue { get; set; }
    }
}
