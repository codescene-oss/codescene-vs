// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IGitChangeLister
    {
        event EventHandler<HashSet<string>> FilesDetected;

        Task<HashSet<string>> GetAllChangedFilesAsync(string gitRootPath, string workspacePath);

        Task<HashSet<string>> GetChangedFilesVsMergeBaseAsync(string gitRootPath, string workspacePath);

        void Initialize(string gitRootPath, string workspacePath);

        void StartPeriodicScanning();

        void StopPeriodicScanning();

        Task<HashSet<string>> CollectFilesFromRepoStateAsync(string gitRootPath, string workspacePath);
    }
}
