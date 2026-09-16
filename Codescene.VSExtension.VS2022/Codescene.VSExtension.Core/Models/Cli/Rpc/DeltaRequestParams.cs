// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    internal class DeltaRequestParams
    {
        [JsonProperty("old-score", NullValueHandling = NullValueHandling.Ignore)]
        public string OldScore { get; set; }

        [JsonProperty("new-score", NullValueHandling = NullValueHandling.Ignore)]
        public string NewScore { get; set; }
    }
}
