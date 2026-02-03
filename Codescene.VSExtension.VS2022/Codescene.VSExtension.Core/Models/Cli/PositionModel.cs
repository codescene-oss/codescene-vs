using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli
{
    public class PositionModel
    {
        [JsonProperty("line")]
        public int Line { get; set; }

        [JsonProperty("character")]
        public int Character { get; set; }
    }
}
