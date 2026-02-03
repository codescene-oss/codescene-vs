// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.Cache.AceRefactorableFunctions
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
    }
}
