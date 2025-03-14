using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.ReviewResultModel;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public interface ICliExecuter
    {
        ReviewMapModel Review(string path);
        void AddToActiveReviewList(string documentPath);
        void AddToActiveReviewList(string documentPath, string content);
        void RemoveFromActiveReviewList(string documentPath);
        ReviewMapModel GetReviewObject(string filePath);
        List<ReviewModel> GetTaggerItems(string filePath);
        string GetFileVersion();
    }
}
