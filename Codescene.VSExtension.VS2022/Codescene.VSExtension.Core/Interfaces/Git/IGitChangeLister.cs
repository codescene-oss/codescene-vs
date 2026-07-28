// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Git;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IGitChangeLister
    {
        event EventHandler<HashSet<string>> FilesDetected;

        Task<HashSet<string>> GetAllChangedFilesAsync(string gitRootPath, string workspacePath, CancellationToken cancellationToken = default);

        Task<HashSet<string>> GetChangedFilesVsMergeBaseAsync(string gitRootPath, string workspacePath, CancellationToken cancellationToken = default);

        void Initialize(string gitRootPath, IReadOnlyCollection<string> workspacePaths, DefaultBranchGate defaultBranchGate = null);

        void SetWorkspacePaths(IReadOnlyCollection<string> workspacePaths);

        void StartPeriodicScanning(CancellationToken cancellationToken);

        void StopPeriodicScanning();

        Task<HashSet<string>> CollectFilesFromRepoStateAsync(string gitRootPath, IReadOnlyCollection<string> workspacePaths, CancellationToken cancellationToken = default);

        void InvalidateDefaultBranchCache();
    }
}
