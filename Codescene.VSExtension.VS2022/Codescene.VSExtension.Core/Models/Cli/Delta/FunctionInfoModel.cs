using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Delta
{
    public class FunctionInfoModel
    {
        /// <summary>
        /// Name of function
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Full range of the function.
        /// </summary>
        [JsonProperty("range")]
        public CliRangeModel Range { get; set; }
    }
}
