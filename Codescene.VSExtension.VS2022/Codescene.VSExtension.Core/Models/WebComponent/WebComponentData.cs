using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.WebComponent
{
    public class WebComponentData
    {
        public bool Loading { get; set; }
        public WebComponentFileData FileData { get; set; }
        public RefactorResponseModel AceResultData { get; set; }
    }
}
