// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Util;
using Codescene.VSExtension.VS2022.Application.Git;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codescene.VSExtension.VS2022.Util;

public static class WorkspacePathResolver
{
    public static async Task<IReadOnlyCollection<string>> GetWorkspaceRootsAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var solution = await VS.Solutions.GetCurrentSolutionAsync();
        var solutionPath = solution?.FullPath;
        if (string.IsNullOrEmpty(solutionPath))
        {
            return null;
        }

        var vsSolution = ServiceProvider.GlobalProvider.GetService(typeof(SVsSolution)) as IVsSolution;
        if (vsSolution == null)
        {
            return null;
        }

        var directories = SolutionProjectDiscovery.GetProjectDirectories(vsSolution, solutionPath);
        if (directories == null || directories.Count == 0)
        {
            return null;
        }

        return directories.ToList();
    }

    public static async Task<bool> IsAllowedWorkspaceFilePathAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return false;
        }

        var workspaceRoots = await GetWorkspaceRootsAsync();
        return WorkspaceFilePathValidator.IsAllowedWorkspaceFilePath(filePath, workspaceRoots);
    }
}
