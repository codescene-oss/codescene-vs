// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codescene.VSExtension.VS2022.Application.Git;

public static class SolutionProjectDiscovery
{
    private static readonly Guid SolutionFolderGuid = new Guid("2150E333-8FDC-42A3-9474-1A3956D46DE8");

    public static HashSet<string> GetProjectDirectories(IVsSolution solution, string solutionPath)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TryAddSolutionDirectory(directories, solutionPath);
        if (solution == null)
        {
            return NormalizePaths(directories);
        }

        ThreadHelper.ThrowIfNotOnUIThread();
        AddProjectDirectoriesFromSolution(solution, directories);
        return NormalizePaths(directories);
    }

    public static string GetProjectDirectory(IVsHierarchy hierarchy)
    {
        if (hierarchy == null)
        {
            return null;
        }

        ThreadHelper.ThrowIfNotOnUIThread();
        if (IsSolutionFolder(hierarchy))
        {
            return null;
        }

        return NormalizeExistingDirectory(TryGetProjectDirProperty(hierarchy) ?? TryGetCanonicalDirectory(hierarchy));
    }

    private static void TryAddSolutionDirectory(HashSet<string> directories, string solutionPath)
    {
        if (string.IsNullOrEmpty(solutionPath))
        {
            return;
        }

        string solutionDir;
        if (Directory.Exists(solutionPath))
        {
            solutionDir = Path.GetFullPath(solutionPath);
        }
        else
        {
            var dirName = Path.GetDirectoryName(solutionPath);
            if (string.IsNullOrEmpty(dirName))
            {
                return;
            }

            solutionDir = Path.GetFullPath(dirName);
        }

        if (string.IsNullOrEmpty(solutionDir))
        {
            return;
        }

        directories.Add(solutionDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    private static void AddProjectDirectoriesFromSolution(IVsSolution solution, HashSet<string> directories)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var guid = Guid.Empty;
        var flags = (uint)(__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION | __VSENUMPROJFLAGS.EPF_UNLOADEDINSOLUTION);
        var hr = solution.GetProjectEnum(flags, ref guid, out var enumHierarchies);
        if (hr != VSConstants.S_OK || enumHierarchies == null)
        {
            return;
        }

        var hierarchy = new IVsHierarchy[1];
        while (enumHierarchies.Next(1, hierarchy, out var fetched) == VSConstants.S_OK && fetched == 1)
        {
            var dir = GetProjectDirectory(hierarchy[0]);
            if (dir != null)
            {
                directories.Add(dir);
            }
        }
    }

    private static string TryGetProjectDirProperty(IVsHierarchy hierarchy)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var hr = hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ProjectDir, out var value);
        if (hr != VSConstants.S_OK)
        {
            return null;
        }

        return value as string;
    }

    private static string TryGetCanonicalDirectory(IVsHierarchy hierarchy)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var hr = hierarchy.GetCanonicalName(VSConstants.VSITEMID_ROOT, out var name);
        if (hr != VSConstants.S_OK || string.IsNullOrEmpty(name))
        {
            return null;
        }

        return Directory.Exists(name) ? name : Path.GetDirectoryName(name);
    }

    private static bool IsSolutionFolder(IVsHierarchy hierarchy)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        var hr = hierarchy.GetGuidProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_TypeGuid, out var typeGuid);
        return hr == VSConstants.S_OK && typeGuid == SolutionFolderGuid;
    }

    private static string NormalizeExistingDirectory(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        return Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static HashSet<string> NormalizePaths(HashSet<string> paths)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var directory = NormalizeExistingDirectory(path);
            if (directory != null)
            {
                normalized.Add(directory);
            }
        }

        return normalized;
    }
}
