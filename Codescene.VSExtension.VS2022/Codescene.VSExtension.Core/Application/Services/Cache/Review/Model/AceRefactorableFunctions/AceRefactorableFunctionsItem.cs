using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions
{
    public class AceRefactorableFunctionsItem
    {
        public string FileContentsHash { get; }
        public List<FnToRefactorModel> Result { get; }

        public AceRefactorableFunctionsItem(string fileContentsHash, List<FnToRefactorModel> result)
        {
            FileContentsHash = fileContentsHash;
            Result = result;
        }
    }
}
