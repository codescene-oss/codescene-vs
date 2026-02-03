using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli
{
    public class CliRangeModel
    {
        /// <summary>
        /// Range start line. 1-indexed.
        /// </summary>
        [JsonProperty("start-line")]
        public int StartLine { get; set; }

        /// <summary>
        /// Range start column. 1-indexed.
        /// </summary>
        [JsonProperty("start-column")]
        public int StartColumn { get; set; }

        /// <summary>
        /// Range end line. 1-indexed.
        /// </summary>
        [JsonProperty("end-line")]
        public int EndLine { get; set; }

        /// <summary>
        /// Range end column. 1-indexed.
        /// </summary>
        [JsonProperty("end-column")]
        public int EndColumn { get; set; }
    }
}
