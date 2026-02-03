using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class RefactoringTargetModel
    {
        [JsonProperty("category")]
        public string Category { get; set; }

        /// <summary>
        /// Gets or sets start line for the code smell.
        /// </summary>
        [JsonProperty("line")]
        public int Line { get; set; }
    }
}
