using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class ReasonDetailsModel
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets 2-tuple pointing to the start-line and end-line of the issue. 0-based.
        /// </summary>
        [JsonProperty("lines")]
        public int[] Lines { get; set; }

        /// <summary>
        /// Gets or sets 2-tuple pointing to the start-col and end-col of the issue. 0-based.
        /// </summary>
        [JsonProperty("columns")]
        public int[] Columns { get; set; }
    }
}
