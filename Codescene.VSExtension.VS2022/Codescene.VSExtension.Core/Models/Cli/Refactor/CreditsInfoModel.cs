using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class CreditsInfoModel
    {
        [JsonProperty("limit")]
        public int Limit { get; set; }

        /// <summary>
        /// Credit reset date in ISO-8601 format
        /// </summary>
        [JsonProperty("reset")]
        public string Reset { get; set; }

        [JsonProperty("used")]
        public int Used { get; set; }
    }
}
