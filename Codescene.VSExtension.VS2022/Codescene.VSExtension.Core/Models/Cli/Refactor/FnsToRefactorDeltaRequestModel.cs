using Codescene.VSExtension.Core.Models.Cli.Delta;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class FnsToRefactorDeltaRequestModel : FnsToRefactorRequestModel
    {
        [JsonProperty("delta-result")]
        public DeltaResponseModel DeltaResult { get; set; }
    }
}
