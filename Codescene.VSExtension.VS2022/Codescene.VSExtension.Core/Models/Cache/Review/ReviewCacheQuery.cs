namespace Codescene.VSExtension.Core.Models.Cache.Review
{
    public class ReviewCacheQuery
    {
        public string FileContents { get; }
        public string FilePath { get; }

        public ReviewCacheQuery(string fileContents, string filePath)
        {
            FileContents = fileContents;
            FilePath = filePath;
        }
    }

}
