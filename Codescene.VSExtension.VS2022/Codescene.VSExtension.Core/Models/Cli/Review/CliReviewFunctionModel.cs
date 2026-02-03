using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Review
{
    public class CliReviewFunctionModel
    {
        /// <summary>
        /// Gets or sets the name of the function which has codesmell(s).
        /// </summary>
        [JsonProperty("function")]
        public string Function { get; set; }

        /// <summary>
        /// Gets or sets range within the code where the smell occurs.
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
