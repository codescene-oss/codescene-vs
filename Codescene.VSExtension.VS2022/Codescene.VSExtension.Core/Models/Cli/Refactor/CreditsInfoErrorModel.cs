using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class CreditsInfoErrorModel
    {
        [JsonProperty("credits-info")]
        public CreditsInfoModel CreditsInfo { get; set; }

        /// <summary>
        /// Gets or sets error message.
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets trace id for the request, use for debugging requests
        /// </summary>
        [JsonProperty("trace-id")]
        public string TraceId { get; set; }
    }
}
