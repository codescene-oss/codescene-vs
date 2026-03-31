// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class FileDataModel
    {
        public string FileName { get; set; }

        public FunctionModel Fn { get; set; }

        public FnToRefactorModel FnToRefactor { get; set; } = null;
    }
}
