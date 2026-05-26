// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Codescene.VSExtension.Core.Util;

public static class WorkspaceFilePathValidator
{
    public static bool HasUnsafePathSyntax(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return true;
        }

        if (path.IndexOfAny(new[] { '|', '>', '<', '*', '?' }) >= 0)
        {
            return true;
        }

        var segments = path.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.None);
        if (segments.Any(segment => segment == ".."))
        {
            return true;
        }

        var root = Path.GetPathRoot(path);
        var pathWithoutRoot = string.IsNullOrEmpty(root) ? path : path.Substring(root.Length);
        if (pathWithoutRoot.IndexOf(':') >= 0)
        {
            return true;
        }

        return false;
    }

    public static bool IsAllowedWorkspaceFilePath(string filePath, IReadOnlyCollection<string> workspaceRoots)
    {
        if (HasUnsafePathSyntax(filePath) || workspaceRoots == null || workspaceRoots.Count == 0)
        {
            return false;
        }

        if (Path.IsPathRooted(filePath))
        {
            return GitPathHelper.IsPathUnderAnyRoot(filePath, workspaceRoots);
        }

        foreach (var root in workspaceRoots)
        {
            if (string.IsNullOrEmpty(root))
            {
                continue;
            }

            try
            {
                var absolutePath = Path.GetFullPath(Path.Combine(root, filePath));
                if (GitPathHelper.IsPathUnderAnyRoot(absolutePath, workspaceRoots))
                {
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }
}
