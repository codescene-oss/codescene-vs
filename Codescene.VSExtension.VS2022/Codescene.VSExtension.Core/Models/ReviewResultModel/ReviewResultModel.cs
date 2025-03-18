using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Models.ReviewResultModel
{
    public class ReviewResultModel
    {
        [JsonProperty("score")]
        public float? Score { get; set; }

        [JsonProperty("file-level-code-smells")]
        public List<CodeSmellModel> FileLevelCodeSmells { get; set; }

        [JsonProperty("function-level-code-smells")]
        public List<ReviewFunctionModel> FunctionLevelCodeSmells { get; set; }

        [JsonProperty("raw-score")]
        public string RawScore { get; set; }
    }
}


