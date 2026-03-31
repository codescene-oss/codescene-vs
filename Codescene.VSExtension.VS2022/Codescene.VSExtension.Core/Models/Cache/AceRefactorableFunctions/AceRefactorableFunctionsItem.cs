// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.Cache.AceRefactorableFunctions
{
    public class AceRefactorableFunctionsItem
    {
        public AceRefactorableFunctionsItem(string fileContentsHash, IList<FnToRefactorModel> result)
        {
            FileContentsHash = fileContentsHash;
            Result = result;
        }

        public string FileContentsHash { get; }

        public IList<FnToRefactorModel> Result { get; }
    }
}
