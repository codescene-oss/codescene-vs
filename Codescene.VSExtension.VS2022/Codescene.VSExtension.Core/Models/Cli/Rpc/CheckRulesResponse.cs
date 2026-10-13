// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Rpc
{
    public class CheckRulesResponse
    {
        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("failed")]
        public bool Failed { get; set; }

        [JsonProperty("parsing-errors")]
        public IList<string> ParsingErrors { get; set; }
    }
}
