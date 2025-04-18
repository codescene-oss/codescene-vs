using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Review
{
    public class CliReviewFunctionModel
    {
        /// <summary>
        /// The name of the function which has codesmell(s).
        /// </summary>
        [JsonProperty("function")]
        public string Function { get; set; }

        /// <summary>
        /// Range within the code where the smell occurs.
        /// </summary>
        [JsonProperty("range")]
        public CliRangeModel Range { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty("code-smells")]
        public CliCodeSmellModel[] CodeSmells { get; set; }
    }
}


