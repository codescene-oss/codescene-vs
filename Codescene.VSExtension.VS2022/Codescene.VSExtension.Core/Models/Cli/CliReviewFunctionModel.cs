using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli
{
    public class CliReviewFunctionModel
    {
        [JsonProperty("function")]
        public string Function { get; set; }

        [JsonProperty("range")]
        public CliRangeModel Range { get; set; }

        [JsonProperty("code-smells")]
        public CliCodeSmellModel[] CodeSmells { get; set; }
    }
}


