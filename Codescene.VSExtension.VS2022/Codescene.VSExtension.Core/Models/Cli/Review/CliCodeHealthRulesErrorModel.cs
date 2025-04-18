using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Review
{
    public class CliCodeHealthRulesErrorModel
    {

        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>
        /// How to resolve the error.
        /// </summary>
        [JsonProperty("remedy")]
        public string Remedy { get; set; }
    }
}
