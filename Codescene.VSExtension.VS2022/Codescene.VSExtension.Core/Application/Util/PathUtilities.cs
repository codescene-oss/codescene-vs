using System;
using System.IO;

namespace Codescene.VSExtension.Core.Application.Util
{
    internal static class PathUtilities
    {
        public static string GetRelativePath(string basePath, string fullPath)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fullPath))
            {
                return fullPath;
            }

            try
            {
                var baseUri = new Uri(AppendDirectorySeparatorChar(basePath));
                var fullUri = new Uri(fullPath);
                var relativeUri = baseUri.MakeRelativeUri(fullUri);
                return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch
            {
                return fullPath;
            }
        }

        public static string AppendDirectorySeparatorChar(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return path + Path.DirectorySeparatorChar;
            }
            return path;
        }
    }
}
