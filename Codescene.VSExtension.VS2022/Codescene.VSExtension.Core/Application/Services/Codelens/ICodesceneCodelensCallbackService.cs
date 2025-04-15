using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.Codelens
{
    public interface ICodesceneCodelensCallbackService
    {
        float GetFileReviewScore(string filePath);
        bool ShowCodeLensForFunction(string issue, string filePath, int startLine);
        int GetVisualStudioPid();
        Task InitializeRpcAsync(string dataPointId);
        bool IsCodeSceneLensesEnabled();
        Task OpenAceToolWindowAsync();
    }
}
