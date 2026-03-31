// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;

namespace Codescene.VSExtension.Core.Models
{
    public sealed class CreateAceComponentDataParams
    {
        public bool Loading { get; set; }

        public string Error { get; set; }

        public bool IsStale { get; set; }

        public WebComponentFileData FileData { get; set; }

        public FnToRefactorModel FnToRefactor { get; set; }

        public RefactorResponseModel AceResultData { get; set; }
    }
}
