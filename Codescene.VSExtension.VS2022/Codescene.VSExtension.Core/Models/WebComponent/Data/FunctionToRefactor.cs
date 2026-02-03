// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class FunctionToRefactor
    {
        public string Name { get; set; }

        public string Body { get; set; }

        public string NippyB64 { get; set; }

        public string FunctionType { get; set; }

        public CodeRangeModel Range { get; set; }

        public List<RefactoringTargetModel> RefactoringTargets { get; set; }
    }
}
