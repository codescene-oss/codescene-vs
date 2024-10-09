using System;
using System.Threading.Tasks;

namespace CodeLensShared
{
    public interface ICodeLevelMetricsCallbackService
    {
        Task<CsReview> GetFileReviewData();
        Task<bool> HasComplexConditionalIssue(Guid projectGuid, string elementDescription, string filePath, int start, int end, dynamic obj);
        int GetVisualStudioPid();
        Task InitializeRpcAsync(string dataPointId);
        Task<bool> IsCodeSceneLensesEnabled();
    }
}
