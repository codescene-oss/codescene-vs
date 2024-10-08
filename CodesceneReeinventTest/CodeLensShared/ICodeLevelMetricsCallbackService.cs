using System.Threading.Tasks;

namespace CodeLensShared
{
    public interface ICodeLevelMetricsCallbackService
    {
        Task<CsReview> GetFileReviewData();

        int GetVisualStudioPid();
        Task InitializeRpcAsync(string dataPointId);
    }
}
