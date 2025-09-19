using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    /// <summary>
    /// A structure for use in subsequent calls to the refactor endpoint.
    /// </summary>
    public class FnToRefactorModel
    {
        /// <summary>
        /// Function name (for presentation)
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("file-type")]
        public string FileType { get; set; }

        //[JsonProperty("function-type")]
        //public string FunctionType { get; set; }

        /// <summary>
        /// Range of the function. Use to keep track of what code to replace in the original file.
        /// </summary>
        [JsonProperty("range")]
        public CliRangeModel Range { get; set; }

        [JsonProperty("refactoring-targets")]
        public RefactoringTargetModel[] RefactoringTargets { get; set; }
    }
}
