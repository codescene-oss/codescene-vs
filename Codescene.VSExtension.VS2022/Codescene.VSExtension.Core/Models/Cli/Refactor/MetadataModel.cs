using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class MetadataModel
    {
        [JsonProperty("cached?")]
        public bool Cached { get; set; }
    }
}
