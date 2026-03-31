// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.VS2022.Tagger
{
    public class UnderlineTaggerTooltipParams(string category, string details, string path, CodeRangeModel range, string functionName, CodeRangeModel functionRange)
    {
        public string Category { get; } = category;

        public string Details { get; } = details;

        public string Path { get; } = path;

        public CodeRangeModel Range { get; } = range;

        public CodeRangeModel FunctionRange { get; } = functionRange;

        public string FunctionName { get; } = functionName;
    }
}
