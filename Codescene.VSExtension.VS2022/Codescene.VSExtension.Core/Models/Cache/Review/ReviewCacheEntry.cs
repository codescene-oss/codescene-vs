namespace Codescene.VSExtension.Core.Models.Cache.Review
{
    public class ReviewCacheEntry
    {
        public string FileContents { get; }
        public string FilePath { get; }
        public FileReviewModel Response { get; }

        public ReviewCacheEntry(string fileContents, string filePath, FileReviewModel response)
        {
            FileContents = fileContents;
            FilePath = filePath;
            Response = response;
        }
    }
}
