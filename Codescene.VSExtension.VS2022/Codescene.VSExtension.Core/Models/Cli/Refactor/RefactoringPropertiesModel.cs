using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class RefactoringPropertiesModel
    {
        [JsonProperty("added-code-smells")]
        public string[] AddedCodeSmells { get; set; }

        [JsonProperty("removed-code-smells")]
        public string[] RemovedCodeSmells { get; set; }
    }
}
