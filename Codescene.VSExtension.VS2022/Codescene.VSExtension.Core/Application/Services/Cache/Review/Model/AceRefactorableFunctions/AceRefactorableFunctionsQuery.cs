namespace Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions
{
    public class AceRefactorableFunctionsQuery
    {
        public string FilePath { get; }
        public string FileContents { get; }

        public AceRefactorableFunctionsQuery(string filePath, string fileContents)
        {
            FileContents = fileContents;
            FilePath = filePath;
        }
    }
}
