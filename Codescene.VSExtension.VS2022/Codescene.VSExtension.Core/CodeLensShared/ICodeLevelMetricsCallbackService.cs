using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.CodeLensShared
{
    public interface ICodeLevelMetricsCallbackService
    {
        float GetFileReviewScore(string filePath);
        bool ShowCodeLensForIssue(string issue, string filePath, int startLine, dynamic obj);
        int GetVisualStudioPid();
        Task InitializeRpcAsync(string dataPointId);
        bool IsCodeSceneLensesEnabled();
    }
}
