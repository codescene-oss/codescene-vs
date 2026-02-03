namespace Codescene.VSExtension.Core.Models.Cache.Delta
{
    public class DeltaCacheQuery
    {
        public string FilePath { get; }

        public string BaselineContent { get; }

        public string CurrentContent { get; }

        public DeltaCacheQuery(string filePath, string baselineContent, string currentContent)
        {
            FilePath = filePath;
            BaselineContent = baselineContent;
            CurrentContent = currentContent;
        }
    }
}
