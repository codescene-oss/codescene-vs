using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Delta
{
    public class FunctionFindingModel
    {
        [JsonProperty("change-details")]
        public ChangeDetailModel[] ChangeDetails { get; set; }

        [JsonProperty("function")]
        public FunctionInfoModel Function { get; set; }

        /// <summary>
        /// Present if the function finding is deemed refactorable.
        /// </summary>
        [JsonProperty("refactorableFn")]
        public FnToRefactorModel RefactorableFn { get; set; }
    }
}
