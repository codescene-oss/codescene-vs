using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    /// <summary>
    /// A structure for use in subsequent calls to the refactor endpoint.
    /// </summary>
    public class FnToRefactorModel
    {
        /// <summary>
        /// Gets or sets function name (for presentation).
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("file-type")]
        public string FileType { get; set; }

        // [JsonProperty("function-type")]
        // public string FunctionType { get; set; }
        [JsonProperty("nippy-b64")]
        public string NippyB64 { get; set; }

        /// <summary>
        /// Gets or sets range of the function. Use to keep track of what code to replace in the original file.
        /// </summary>
        [JsonProperty("range")]
        public CliRangeModel Range { get; set; }

        [JsonProperty("refactoring-targets")]
        public RefactoringTargetModel[] RefactoringTargets { get; set; }
    }
}
