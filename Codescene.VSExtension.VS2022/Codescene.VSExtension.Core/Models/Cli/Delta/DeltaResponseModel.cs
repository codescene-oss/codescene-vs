using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Delta
{
    public class DeltaResponseModel
    {
        [JsonProperty("file-level-findings")]
        public ChangeDetailModel[] FileLevelFindings { get; set; }

        /// <summary>
        /// Function level findings also include expression level smells (i.e.Complex Conditionals). For expression level smells the 'function' range might only correspond to the highlighting range - unless the function also contains other smells.
        /// </summary>
        [JsonProperty("function-level-findings")]
        public FunctionFindingModel[] FunctionLevelFindings { get; set; }

        /// <summary>
        /// If file is still present, the new score for the file
        /// </summary>
        [JsonProperty("new-score")]
        public decimal NewScore { get; set; }

        /// <summary>
        /// If the file was not recently created, the old file score
        /// </summary>
        [JsonProperty("old-score")]
        public decimal OldScore { get; set; }

        /// <summary>
        /// Represents the change in score for this Delta. An empty old- or new score is assumed to be 10.0 when comparing.
        /// </summary>
        [JsonProperty("score-change")]
        public decimal ScoreChange { get; set; }
    }
}
