// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;

namespace Codescene.VSExtension.Core.Util
{
    public static class GitPathHelper
    {
        public static bool IsFileInWorkspace(string relativePath, string gitRootPath, string workspacePath)
        {
            try
            {
                var absolutePath = Path.Combine(gitRootPath, relativePath);
                var normalizedAbsolute = Path.GetFullPath(absolutePath);
                var normalizedWorkspace = Path.GetFullPath(workspacePath);
                var workspacePrefix = normalizedWorkspace.EndsWith(Path.DirectorySeparatorChar.ToString())
                    ? normalizedWorkspace
                    : normalizedWorkspace + Path.DirectorySeparatorChar;

                if (!File.Exists(normalizedAbsolute))
                {
                    return false;
                }

                return normalizedAbsolute.StartsWith(workspacePrefix, StringComparison.OrdinalIgnoreCase) ||
                       normalizedAbsolute.Equals(normalizedWorkspace, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsPathUnderAnyRoot(string fullPath, IReadOnlyCollection<string> roots)
        {
            if (roots == null || roots.Count == 0)
            {
                return true;
            }

            var normalizedFull = Path.GetFullPath(fullPath);
            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root))
                {
                    continue;
                }

                var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
                var prefix = normalizedRoot + Path.DirectorySeparatorChar;
                if (normalizedFull.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || normalizedFull.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsFileInWorkspace(string relativePath, string gitRootPath, IReadOnlyCollection<string> workspacePaths)
        {
            if (workspacePaths == null || workspacePaths.Count == 0)
            {
                return true;
            }

            foreach (var workspacePath in workspacePaths)
            {
                if (IsFileInWorkspace(relativePath, gitRootPath, workspacePath))
                {
                    return true;
                }
            }

            return false;
        }

        public static string ConvertToAbsolutePath(string relativePath, string basePath)
        {
            try
            {
                return Path.GetFullPath(Path.Combine(basePath, relativePath));
            }
            catch
            {
                return Path.Combine(basePath, relativePath);
            }
        }
    }
}
