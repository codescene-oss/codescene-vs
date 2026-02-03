using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli
{
    public class CliRangeModel
    {
        /// <summary>
        /// Gets or sets range start line. 1-indexed.
        /// </summary>
        [JsonProperty("start-line")]
        public int StartLine { get; set; }

        /// <summary>
        /// Gets or sets range start column. 1-indexed.
        /// </summary>
        [JsonProperty("start-column")]
        public int StartColumn { get; set; }

        /// <summary>
        /// Gets or sets range end line. 1-indexed.
        /// </summary>
        [JsonProperty("end-line")]
        public int EndLine { get; set; }

        /// <summary>
        /// Gets or sets range end column. 1-indexed.
        /// </summary>
        [JsonProperty("end-column")]
        public int EndColumn { get; set; }
    }
}
