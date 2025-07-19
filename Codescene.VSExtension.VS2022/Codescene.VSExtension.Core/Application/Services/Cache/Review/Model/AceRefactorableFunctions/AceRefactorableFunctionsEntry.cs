using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions
{
    public class AceRefactorableFunctionsEntry
    {
        public string FilePath { get; }
        public string FileContents { get; }
        public IList<FnToRefactorModel> Result { get; }

        public AceRefactorableFunctionsEntry(string filePath, string fileContents, IList<FnToRefactorModel> result)
        {
            FileContents = fileContents;
            FilePath = filePath;
            Result = result;
        }

        public override string ToString()
        {
            string resultSummary;
            if (Result == null)
            {
                resultSummary = "null";
            }
            else
            {
                var items = Result.Select(fn => $"{fn.Name} [{fn.FunctionType}] (Range: {fn.Range?.Startline}-{fn.Range?.EndLine})").ToList();
                resultSummary = string.Join(", ", items);
            }

            return $"FilePath: {FilePath}, Result: [{resultSummary}]";
        }
    }
}
