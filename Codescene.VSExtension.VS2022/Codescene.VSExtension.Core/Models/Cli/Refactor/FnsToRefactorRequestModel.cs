using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public abstract class FnsToRefactorRequestModel
    {
        [JsonProperty("file-name")]
        public string FileName { get; set; }

        [JsonProperty("file-content")]
        public string FileContent { get; set; }

        [JsonProperty("preflight")]
        public PreFlightResponseModel Preflight { get; set; }

        [JsonProperty("cache-path")]
        public string CachePath { get; set; }
    }
}
