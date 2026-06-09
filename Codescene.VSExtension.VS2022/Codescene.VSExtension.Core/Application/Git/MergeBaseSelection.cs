// Copyright (c) CodeScene. All rights reserved.

using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Application.Git
{
    public readonly struct MergeBaseSelection
    {
        public MergeBaseSelection(Commit commit, string baselineBranchName)
        {
            Commit = commit;
            BaselineBranchName = baselineBranchName;
        }

        public Commit Commit { get; }

        public string BaselineBranchName { get; }
    }
}
