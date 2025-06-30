using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ICliExecutor
    {
        CliReviewModel Review(string path);
        CliReviewModel ReviewContent(string filename, string content);
        Task<DeltaResponseModel> ReviewDelta(string content, string oldScore, string newScore);
        string GetFileVersion();
        PreFlightResponseModel Preflight(bool force = true);
        IList<FnToRefactorModel> FnsToRefactorFromCodeSmells(string content, string extension, string codeSmells);
        IList<FnToRefactorModel> FnsToRefactorFromCodeSmells(string content, string extension, string codeSmells, string preflight);
        IList<FnToRefactorModel> FnsToRefactorFromDelta(string content, string extension, string delta);
        IList<FnToRefactorModel> FnsToRefactorFromDelta(string content, string extension, string delta, string preflight);
        Task<RefactorResponseModel> PostRefactoring(string fnToRefactor, bool skipCache = false, string token = null);
        Task<IList<FnToRefactorModel>> FnsToRefactorFromCodeSmellsAsync(string content, string extension, string codeSmells);
        Task<IList<FnToRefactorModel>> FnsToRefactorFromCodeSmellsAsync(string content, string extension, string codeSmells, string preflight);
    }
}
