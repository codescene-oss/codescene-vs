// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.Core.Application;

public static class CodeSmellLookup
{
    public static CodeSmellModel FirstOverlapping(IEnumerable<CodeSmellModel> smells, int startLine, int endLine)
    {
        if (smells == null)
        {
            return null;
        }

        return smells.FirstOrDefault(smell =>
            smell.Range.StartLine <= endLine && smell.Range.EndLine >= startLine);
    }
}
