using Codescene.VSExtension.Core.Models.ReviewResult;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Models.ReviewResultModel
{
    public class ReviewResultModel
    {
        public float Score { get; set; }
        [JsonProperty("file-level-code-smells")]
        public List<CodeSmellModel> FileLevelCodeSmells { get; set; }
        [JsonProperty("function-level-code-smells")]
        public List<FunctionLevelCodeSmellModel> FunctionLevelCodeSmells { get; set; }
        [JsonProperty("expression-level-code-smells")]
        public List<CodeSmellModel> ExpressionLevelCodeSmells { get; set; }
        public RawScore RawScore { get; set; }
    }
}


