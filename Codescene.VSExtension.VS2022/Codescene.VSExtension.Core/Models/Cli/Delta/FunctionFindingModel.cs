using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

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

        public override string ToString()
        {
            var changeDetailsStr = ChangeDetails == null
                ? "null"
                : $"[{string.Join(", ", ChangeDetails.Select(cd => $"{{Category: {cd.Category}, Type: {cd.ChangeType}, Desc: {cd.Description}, Line: {cd.Line}}}"))}]";

            var functionStr = Function == null
                ? "null"
                : $"{{Name: {Function.Name}, Range: {(Function.Range != null ? $"({Function.Range.Startline},{Function.Range.EndLine})-({Function.Range.StartColumn},{Function.Range.EndColumn})" : "null")}}}";

            var refactorableFnStr = RefactorableFn == null
                ? "null"
                : $"{{Name: {RefactorableFn.Name}, FileType: {RefactorableFn.FileType}, Range: {(RefactorableFn.Range != null ? $"({RefactorableFn.Range.Startline},{RefactorableFn.Range.EndLine})-({RefactorableFn.Range.StartColumn},{RefactorableFn.Range.EndColumn})" : "null")}}}";

            return $"FunctionFindingModel {{ ChangeDetails: {changeDetailsStr}, Function: {functionStr}, RefactorableFn: {refactorableFnStr} }}";
        }
    }
}
