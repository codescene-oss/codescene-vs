using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.ReviewResultModel
{
    public class ReviewFunctionModel
    {
        [JsonProperty("function")]
        public string Function { get; set; }

        [JsonProperty("range")]
        public RangeModel Range { get; set; }

        [JsonProperty("code-smells")]
        public CodeSmellModel[] CodeSmells { get; set; }
    }
}


