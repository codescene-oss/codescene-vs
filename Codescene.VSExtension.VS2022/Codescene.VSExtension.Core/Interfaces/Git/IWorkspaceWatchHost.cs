// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Models.Cli.Rpc;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IWorkspaceWatchHost
    {
        IReadOnlyList<ReviewDocument> GetDirtyDocuments();

        void PruneMonitor(IReadOnlyList<string> repoRoots, ISet<string> keepPaths);
    }
}
