using System.Threading.Tasks;

namespace CodeLensShared
{
    public interface ICodeLevelMetricsCallbackService
    {
        Task<string> GetFileCodeHealth();
        int GetVisualStudioPid();
        Task InitializeRpcAsync(string dataPointId);
    }
}
