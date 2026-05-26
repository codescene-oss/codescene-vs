// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class RefactorPostRequestModel
    {
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("skip-cache")]
        public bool? SkipCache { get; set; }

        [JsonProperty("fn-to-refactor-nippy-b64")]
        public string FnToRefactorNippyB64 { get; set; }

        [JsonProperty("fn-to-refactor")]
        public FnToRefactorModel FnToRefactor { get; set; }
    }
}
