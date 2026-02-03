using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Review
{
    public class ReviewRequestModel
    {
        [JsonProperty("path")]
        public string FilePath { get; set; }

        [JsonProperty("file-content")]
        public string FileContent { get; set; }

        [JsonProperty("cache-path")]
        public string CachePath { get; set; }
    }
}
