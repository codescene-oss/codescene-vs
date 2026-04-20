// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Git
{
    public static class MainBranchNames
    {
        public static readonly IReadOnlyList<string> All = new[]
        {
            "main", "master", "develop", "development", "trunk", "dev",
        };

        public static bool IsMainBranch(string branchName)
        {
            return !string.IsNullOrEmpty(branchName)
                && All.Contains(branchName, StringComparer.OrdinalIgnoreCase);
        }
    }
}
