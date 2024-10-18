using CodesceneReeinventTest.Core.Models;
using Core.Models.ReviewResultModel;
using System.Collections.Generic;

namespace Core.Application.Services.FileReviewer
{
    public interface IFileReviewer
    {
        ReviewResultModel Review(string path);
        void AddToActiveReviewList(string documentPath);
        void RemoveFromActiveReviewList(string documentPath);
        ReviewResultModel GetReviewObject(string filePath);
        List<TaggerItemModel> GetTaggerItems(string filePath);
    }
}
