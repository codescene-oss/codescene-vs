// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.Core.Application;

public static class TooltipSmellBuilder
{
    public static CodeSmellModel Build(string category, CodeRangeModel range, string functionName, CodeRangeModel functionRange)
    {
        return new CodeSmellModel
        {
            Category = category,
            Range = range,
            FunctionName = functionName,
            FunctionRange = functionRange,
        };
    }
}
