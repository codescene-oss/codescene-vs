using Core.Models.ReviewResult;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

namespace Core.Models.ReviewResultModel
{
    public class ReviewResultModel
    {
        public float Score { get; set; }
        [JsonProperty("file-level-code-smells")]
        public List<FileLevelCodeSmellModel> FileLevelCodeSmells { get; set; }
        [JsonProperty("function-level-code-smells")]
        public List<FunctionLevelCodeSmellModel> FunctionLevelCodeSmells { get; set; }
        [JsonProperty("expression-level-code-smells")]
        public List<ExpressionLevelCodeSmellModel> ExpressionLevelCodeSmells { get; set; }
        public RawScore RawScore { get; set; }
    }
}


