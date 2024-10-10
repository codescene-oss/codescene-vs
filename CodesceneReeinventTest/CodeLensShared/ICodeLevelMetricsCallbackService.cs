using System.Threading.Tasks;

namespace CodeLensShared
{
    public interface ICodeLevelMetricsCallbackService
    {
        Task<CsReview> GetFileReviewData();
        bool ShowCodeLensForIssue(string issue, string filePath, int startLine);
        int GetVisualStudioPid();
        Task InitializeRpcAsync(string dataPointId);
        Task<bool> IsCodeSceneLensesEnabled();
    }
}
