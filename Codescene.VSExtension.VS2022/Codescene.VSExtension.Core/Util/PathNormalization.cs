// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;

namespace Codescene.VSExtension.Core.Util
{
    public static class PathNormalization
    {
        public static string NormalizeWorkingDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            var trimmedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root))
            {
                return trimmedPath;
            }

            var trimmedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(trimmedPath, trimmedRoot, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            return trimmedPath;
        }
    }
}
