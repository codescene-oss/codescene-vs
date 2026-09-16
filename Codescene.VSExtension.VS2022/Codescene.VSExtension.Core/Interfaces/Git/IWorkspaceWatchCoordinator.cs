// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IWorkspaceWatchCoordinator
    {
        Task SyncAsync(IReadOnlyList<string> workspaceDirectories);

        void StopAll();

        IReadOnlyCollection<string> GetInventoryFiles(string repoRoot);
    }
}
