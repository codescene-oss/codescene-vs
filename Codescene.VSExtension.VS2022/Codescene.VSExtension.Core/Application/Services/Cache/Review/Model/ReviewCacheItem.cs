using Codescene.VSExtension.Core.Models.ReviewModels;

namespace Codescene.VSExtension.Core.Application.Services.Cache.Review.Model
{
    public class ReviewCacheItem
    {
        public string FileContentsHash { get; }
        public FileReviewModel Response { get; }

        public ReviewCacheItem(string fileContentsHash, FileReviewModel response)
        {
            FileContentsHash = fileContentsHash;
            Response = response;
        }
    }
}
