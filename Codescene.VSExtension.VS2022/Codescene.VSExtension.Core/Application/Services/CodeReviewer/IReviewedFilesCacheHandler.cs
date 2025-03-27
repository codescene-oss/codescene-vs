using Codescene.VSExtension.Core.Models.ReviewModels;

namespace Codescene.VSExtension.Core.Application.Services.CodeReviewer
{
    public interface IReviewedFilesCacheHandler
    {
        void Add(FileReviewModel model);
        FileReviewModel Get(string path);
        bool Remove(string path);
        bool Exists(string path);
        //void AddToActiveReviewList(string documentPath);
        //void AddToActiveReviewList(string documentPath, string content);
        //void RemoveFromActiveReviewList(string documentPath);
        ////List<ReviewModel> GetTaggerItems(string filePath);
        //ReviewMapModel GetReviewObject(string filePath);
    }
}
