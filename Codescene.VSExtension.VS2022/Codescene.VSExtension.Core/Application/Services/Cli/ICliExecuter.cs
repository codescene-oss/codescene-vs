using Codescene.VSExtension.Core.Models.Cli;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ICliExecuter
    {
        CliReviewModel Review(string path);
        CliReviewModel ReviewContent(string filename, string content);
        //void AddToActiveReviewList(string documentPath);
        //void AddToActiveReviewList(string documentPath, string content);
        //void RemoveFromActiveReviewList(string documentPath);
        //ReviewMapModel GetReviewObject(string filePath);
        //List<ReviewModel> GetTaggerItems(string filePath);
        string GetFileVersion();
    }
}
