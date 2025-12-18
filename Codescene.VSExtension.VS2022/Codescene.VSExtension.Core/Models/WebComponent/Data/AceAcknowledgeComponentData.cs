using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class AceAcknowledgeComponentData
    {
        public string FilePath { get; set; }
        public AutoRefactorConfig AutoRefactor { get; set; }
        public FnToRefactorModel FnToRefactor {  get; set; }
    }
}