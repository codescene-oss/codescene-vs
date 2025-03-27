using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli
{
    public class CliRangeModel
    {
        [JsonProperty("start-line")]
        public int Startline { get; set; }

        [JsonProperty("start-column")]
        public int StartColumn { get; set; }

        [JsonProperty("end-line")]
        public int EndLine { get; set; }

        [JsonProperty("end-column")]
        public int EndColumn { get; set; }
    }
}
