using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.WebComponent
{
    public class WebComponentPayload
    {
        public string IdeType { get; set; }
        public string View { get; set; }
        public WebComponentData Data { get; set; }
        public RefactorResponseModel AceResultData { get; set; }
    }
}
