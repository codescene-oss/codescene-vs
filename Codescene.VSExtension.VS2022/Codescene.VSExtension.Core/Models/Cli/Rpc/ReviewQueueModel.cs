// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class ReviewQueueModel
    {
        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("files")]
        public IList<string> Files { get; set; }
    }
}
