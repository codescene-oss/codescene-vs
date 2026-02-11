// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Models.WebComponent.Data
{
    public class FunctionFinding
    {
        public Function Function { get; set; }

        public List<ChangeDetail> ChangeDetails { get; set; }

        public FunctionToRefactor RefactorableFn { get; set; }
    }
}
