// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using Codescene.VSExtension.Core.Application.Util;

namespace Codescene.VSExtension.Core.Util
{
    public static class RpcPath
    {
        public static string NormalizeFsPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return filePath ?? string.Empty;
            }

            try
            {
                var normalized = Path.GetFullPath(filePath.Replace('/', Path.DirectorySeparatorChar));
                return PathNormalization.NormalizeWorkingDirectory(normalized).ToLowerInvariant();
            }
            catch
            {
                return filePath.Replace('/', Path.DirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant();
            }
        }

        public static string ToPosixRelPath(string relPath)
        {
            return (relPath ?? string.Empty).Replace('\\', '/');
        }

        public static string RelativePosix(string repoRoot, string absolutePath)
        {
            return ToPosixRelPath(PathUtilities.GetRelativePath(repoRoot, absolutePath));
        }

        public static bool PathsEqual(string left, string right)
        {
            return string.Equals(NormalizeFsPath(left), NormalizeFsPath(right), StringComparison.Ordinal);
        }

        public static string CombineRepo(string repoRoot, string posixRelPath)
        {
            var relative = (posixRelPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(repoRoot ?? string.Empty, relative);
        }
    }
}
