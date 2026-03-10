// Copyright (c) CodeScene. All rights reserved.

using System;
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
