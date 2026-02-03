namespace Codescene.VSExtension.Core.Models.Cache.AceRefactorableFunctions
{
    public class AceRefactorableFunctionsQuery
    {
        public string FilePath { get; }
        public string FileContents { get; }

        public AceRefactorableFunctionsQuery(string filePath, string fileContents)
        {
            FilePath = filePath;
            FileContents = fileContents;
        }
    }
}
