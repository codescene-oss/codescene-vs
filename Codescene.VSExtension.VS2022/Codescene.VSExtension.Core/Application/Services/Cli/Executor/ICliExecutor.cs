using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ICliExecutor
    {
        CliReviewModel ReviewContent(string filename, string content);
        DeltaResponseModel ReviewDelta(string oldScore, string newScore);
        string GetFileVersion();
        string GetDeviceId();
        PreFlightResponseModel Preflight(bool force = true);
        RefactorResponseModel PostRefactoring(FnToRefactorModel fnToRefactor, bool skipCache = false, string token = null);
        IList<FnToRefactorModel> FnsToRefactorFromCodeSmells(string content, string extension, string codeSmells, string preflight);
    }
}
