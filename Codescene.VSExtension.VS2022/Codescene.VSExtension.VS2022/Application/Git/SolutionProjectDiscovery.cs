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
    public static HashSet<string> GetProjectDirectories(IVsSolution solution, string solutionPath)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TryAddSolutionDirectory(directories, solutionPath);
        if (solution == null)
        {
            return DeduplicatePaths(directories);
        }

        ThreadHelper.ThrowIfNotOnUIThread();
        AddProjectDirectoriesFromSolution(solution, directories);
        return DeduplicatePaths(directories);
    }

    public static string GetProjectDirectory(IVsHierarchy hierarchy)
    {
        if (hierarchy == null)
        {
            return null;
        }

        ThreadHelper.ThrowIfNotOnUIThread();

        var hr = hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ProjectDir, out var value);
        if (hr != VSConstants.S_OK || value == null)
        {
            return null;
        }

        var dir = value as string;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            return null;
        }

        return Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static void TryAddSolutionDirectory(HashSet<string> directories, string solutionPath)
    {
        if (string.IsNullOrEmpty(solutionPath))
        {
            return;
        }

        var solutionDir = Directory.Exists(solutionPath)
            ? Path.GetFullPath(solutionPath)
            : Path.GetFullPath(Path.GetDirectoryName(solutionPath));
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
        var hr = solution.GetProjectEnum((uint)__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION, ref guid, out var enumHierarchies);
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

    private static HashSet<string> DeduplicatePaths(HashSet<string> paths)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            var isChildOfAny = false;
            foreach (var other in paths)
            {
                if (string.Equals(normalized, other.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var otherNorm = other.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
                if (!otherNorm.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    otherNorm += Path.DirectorySeparatorChar;
                }

                if (normalized.StartsWith(otherNorm, StringComparison.OrdinalIgnoreCase))
                {
                    isChildOfAny = true;
                    break;
                }
            }

            if (!isChildOfAny)
            {
                result.Add(normalized);
            }
        }

        return result;
    }
}
