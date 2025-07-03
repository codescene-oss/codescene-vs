using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions
{
    public class AceRefactorableFunctionsEntry
    {
        public string FilePath { get; }
        public string FileContents { get; }
        public List<FnToRefactorModel> Result { get; }

        public AceRefactorableFunctionsEntry(string filePath, string fileContents, List<FnToRefactorModel> result)
        {
            FileContents = fileContents;
            FilePath = filePath;
            Result = result;
        }
    }
}
