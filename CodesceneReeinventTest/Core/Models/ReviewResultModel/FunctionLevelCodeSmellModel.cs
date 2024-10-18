using Newtonsoft.Json;

namespace Core.Models.ReviewResultModel
{
    public class FunctionLevelCodeSmellModel
    {
        [JsonProperty("function")]
        public string Function { get; set; }
        [JsonProperty("range")]
        public RangeModel Range { get; set; }
        [JsonProperty("code-smells")]
        public CodeSmellModel[] CodeSmells { get; set; }
    }
}


