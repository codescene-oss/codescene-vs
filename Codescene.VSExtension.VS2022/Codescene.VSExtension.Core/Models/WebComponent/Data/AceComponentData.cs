// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class AceComponentData
    {
        public bool Loading { get; set; }

        public string Error { get; set; }

        public bool IsStale { get; set; } = false;

        public WebComponentFileDataBase FileData { get; set; }

        public FnToRefactorModel FnToRefactor { get; set; }

        public RefactorResponseModel AceResultData { get; set; }
    }
}
