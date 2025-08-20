using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ICliExecutor
    {
        CliReviewModel ReviewContent(string filename, string content);
        DeltaResponseModel ReviewDelta(string oldScore, string newScore);
        Task<string> GetFileVersionAsync();
        string GetDeviceId();
        Task<PreFlightResponseModel> PreflightAsync(bool force = true);
        Task<RefactorResponseModel> PostRefactoringAsync(string fnToRefactor, bool skipCache = false, string token = null);
        Task<IList<FnToRefactorModel>> FnsToRefactorFromCodeSmellsAsync(string content, string extension, string codeSmells, string preflight);
    }
}
