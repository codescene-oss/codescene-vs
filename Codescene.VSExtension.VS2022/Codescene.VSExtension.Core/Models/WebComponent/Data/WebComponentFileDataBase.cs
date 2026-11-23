// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class WebComponentFileDataBase
    {
        public string FileName { get; set; }

        public WebComponentFileDataBaseFn Fn { get; set; }

        public FnToRefactorModel FnToRefactor { get; set; }
    }
}
