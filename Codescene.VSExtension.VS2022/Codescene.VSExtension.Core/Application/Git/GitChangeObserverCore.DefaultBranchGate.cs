// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Application.Git
{
    public partial class GitChangeObserverCore
    {
        private void InitializeDefaultBranchGate()
        {
            if (!string.IsNullOrEmpty(_gitRootPath))
            {
                _defaultBranchGate = new DefaultBranchGate(_gitRootPath);
            }
        }

        private bool ShouldSkipBasedOnDefaultBranch()
        {
            return _defaultBranchGate?.ShouldSkipForCurrentBranch() ?? false;
        }
    }
}
