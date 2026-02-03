// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class RefactorSupportModel
    {
        [JsonProperty("max-input-loc")]
        public int MaxInputLoc { get; set; }

        [JsonProperty("code-smells")]
        public string[] CodeSmells { get; set; }
    }
}
