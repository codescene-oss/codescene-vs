// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IBatchGitIgnoreChecker
    {
        HashSet<string> FilterIgnored(IEnumerable<string> absolutePaths);
    }
}
