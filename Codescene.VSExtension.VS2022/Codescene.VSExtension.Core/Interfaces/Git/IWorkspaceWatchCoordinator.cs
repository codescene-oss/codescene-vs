// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IWorkspaceWatchCoordinator
    {
        long WatchSession { get; }

        Task SyncAsync(IReadOnlyList<string> workspaceDirectories);

        Task SyncAsync(IReadOnlyList<string> workspaceDirectories, long watchSession);

        void StopAll();

        void SetEnabled(bool enabled);

        IReadOnlyCollection<string> GetInventoryFiles(string repoRoot);
    }
}
