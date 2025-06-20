namespace Codescene.VSExtension.Core.Application.Services.Cache.Review.Model
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
