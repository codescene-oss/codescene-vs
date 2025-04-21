using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ICliExecuter
    {
        CliReviewModel Review(string path);
        CliReviewModel ReviewContent(string filename, string content);
        string GetFileVersion();
        PreFlightResponseModel Preflight(bool force = true);
        RefactorResponseModel FnsToRefactorFromCodeSmells(string extension, string content, string codeSmellsJson);
        RefactorResponseModel FnsToRefactorFromCodeSmells(string extension, string content, string codeSmellsJson, string preflight);
    }
}
