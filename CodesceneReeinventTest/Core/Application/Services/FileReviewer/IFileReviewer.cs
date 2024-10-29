using Core.Models;
using Core.Models.ReviewResultModel;
using System.Collections.Generic;

namespace Core.Application.Services.FileReviewer
{
    public interface IFileReviewer
    {
        ReviewMapModel Review(string path);
        void AddToActiveReviewList(string documentPath);
        void AddToActiveReviewList(string documentPath, string content);
        void RemoveFromActiveReviewList(string documentPath);
        ReviewMapModel GetReviewObject(string filePath);
        List<ReviewModel> GetTaggerItems(string filePath);
    }
}
