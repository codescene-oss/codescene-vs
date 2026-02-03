namespace Codescene.VSExtension.Core.Models.Cache.Review
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
