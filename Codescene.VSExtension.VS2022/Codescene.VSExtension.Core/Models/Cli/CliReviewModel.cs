using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Models.Cli
{
    public class CliReviewModel
    {
        [JsonProperty("score")]
        public float? Score { get; set; }

        [JsonProperty("file-level-code-smells")]
        public List<CliCodeSmellModel> FileLevelCodeSmells { get; set; }

        [JsonProperty("function-level-code-smells")]
        public List<CliReviewFunctionModel> FunctionLevelCodeSmells { get; set; }

        [JsonProperty("raw-score")]
        public string RawScore { get; set; }
    }
}


