using System.Collections.Generic;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface ICliExecutor
    {
        CliReviewModel ReviewContent(string filename, string content);

        DeltaResponseModel ReviewDelta(string oldScore, string newScore);

        string GetFileVersion();

        string GetDeviceId();

        PreFlightResponseModel Preflight(bool force = true);

        RefactorResponseModel PostRefactoring(FnToRefactorModel fnToRefactor, bool skipCache = false, string token = null);

        IList<FnToRefactorModel> FnsToRefactorFromCodeSmells(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight);

        IList<FnToRefactorModel> FnsToRefactorFromDelta(string fileName, string fileContent, DeltaResponseModel deltaResponse, PreFlightResponseModel preflight);
    }
}
