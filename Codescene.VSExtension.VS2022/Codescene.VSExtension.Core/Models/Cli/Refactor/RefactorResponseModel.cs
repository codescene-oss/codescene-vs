using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class RefactorResponseModel
    {
        /// <summary>
        /// Refactored code
        /// </summary>
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("confidence")]
        public ConfidenceModel Confidence { get; set; }

        /// <summary>
        /// ACE Credit info
        /// </summary>
        [JsonProperty("credits-info")]
        public CreditsInfoModel CreditsInfo { get; set; }

        [JsonProperty("metadata")]
        public MetadataModel Metadata { get; set; }

        /// <summary>
        /// List of reasons for refactoring failure
        /// </summary>
        [JsonProperty("reasons")]
        public ReasonModel[] Reasons { get; set; }

        [JsonProperty("refactoring-properties")]
        public RefactoringPropertiesModel RefactoringProperties { get; set; }

        /// <summary>
        /// Trace id for the request, use for debugging requests
        /// </summary>
        [JsonProperty("trace-id")]
        public string TraceId { get; set; }
    }
}
