// Copyright (c) CodeScene. All rights reserved.

using System.Linq;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Application
{
    public static class CachedFindingLookup
    {
        public static FnToRefactorModel FindByFunctionName(DeltaResponseModel delta, string functionName)
        {
            if (delta?.FunctionLevelFindings == null || string.IsNullOrEmpty(functionName))
            {
                return null;
            }

            var finding = delta.FunctionLevelFindings
                .FirstOrDefault(f => f?.Function?.Name == functionName);

            return finding?.RefactorableFn;
        }
    }
}
