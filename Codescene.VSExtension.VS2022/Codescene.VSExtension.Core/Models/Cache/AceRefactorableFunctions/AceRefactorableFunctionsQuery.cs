// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Models.Cache.AceRefactorableFunctions
{
    public class AceRefactorableFunctionsQuery
    {
        public AceRefactorableFunctionsQuery(string filePath, string fileContents)
        {
            FilePath = filePath;
            FileContents = fileContents;
        }

        public string FilePath { get; }

        public string FileContents { get; }
    }
}
