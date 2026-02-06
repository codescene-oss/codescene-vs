// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Git
{
    public interface IGitChangeLister
    {
        Task<HashSet<string>> GetAllChangedFilesAsync(string gitRootPath, string workspacePath);

        Task<HashSet<string>> GetChangedFilesVsMergeBaseAsync(string gitRootPath, string workspacePath);
    }
}
