﻿using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Models.Cli.Review
{
    public class CliReviewModel
    {
        /// <summary>
        /// If file is scorable, this will be a number between 1.0 and 10.0
        /// </summary>
        [JsonProperty("score")]
        public float? Score { get; set; }

        [JsonProperty("file-level-code-smells")]
        public List<CliCodeSmellModel> FileLevelCodeSmells { get; set; }

        [JsonProperty("function-level-code-smells")]
        public List<CliReviewFunctionModel> FunctionLevelCodeSmells { get; set; }

        /// <summary>
        /// Base64 encoded review data used by the delta analysis.
        /// </summary>
        [JsonProperty("raw-score")]
        public string RawScore { get; set; }

        /// <summary>
        /// Optional map with error info about code-health-rules. Present if there were any issues parsing the rules-file.
        /// </summary>
        [JsonProperty("code-health-rules-error")]
        public CliCodeHealthRulesErrorModel CodeHealthRulesError { get; set; }
    }
}


