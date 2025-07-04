using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions
{
    public class AceRefactorableFunctionsItem
    {
        public string FileContentsHash { get; }
        public IList<FnToRefactorModel> Result { get; }

        public AceRefactorableFunctionsItem(string fileContentsHash, IList<FnToRefactorModel> result)
        {
            FileContentsHash = fileContentsHash;
            Result = result;
        }
    }
}
