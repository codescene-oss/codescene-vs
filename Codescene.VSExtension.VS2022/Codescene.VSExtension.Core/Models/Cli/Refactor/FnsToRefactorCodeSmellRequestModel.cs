using System.Collections.Generic;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Models.Cli.Refactor
{
    public class FnsToRefactorCodeSmellRequestModel : FnsToRefactorRequestModel
    {
        [JsonProperty("code-smells")]
        public IList<CliCodeSmellModel> CodeSmells { get; set; }
    }
}
