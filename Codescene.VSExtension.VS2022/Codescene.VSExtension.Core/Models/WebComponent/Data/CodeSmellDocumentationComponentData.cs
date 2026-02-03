// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class CodeSmellDocumentationComponentData
    {
        public string DocType { get; set; }

        public AutoRefactorConfig AutoRefactor { get; set; }

        public FileDataModel FileData { get; set; }
    }
}
