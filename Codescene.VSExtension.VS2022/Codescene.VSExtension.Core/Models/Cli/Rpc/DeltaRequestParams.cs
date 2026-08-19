// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class DeltaRequestParams
    {
        [JsonProperty("old-score")]
        public string OldScore { get; set; }

        [JsonProperty("new-score")]
        public string NewScore { get; set; }
    }
}
