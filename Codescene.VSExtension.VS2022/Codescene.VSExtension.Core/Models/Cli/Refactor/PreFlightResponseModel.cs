using Newtonsoft.Json;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class PreFlightResponseModel
    {
        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("file-types")]
        public string[] FileTypes { get; set; }

        [JsonProperty("language-common")]
        public RefactorSupportModel LanguageCommon { get; set; }

        [JsonProperty("language-specific")]
        public Dictionary<string, RefactorSupportModel> LanguageSpecific { get; set; }
    }
}
