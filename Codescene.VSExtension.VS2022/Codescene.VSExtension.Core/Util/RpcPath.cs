// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Codescene.VSExtension.Core.Application.Util;
using Microsoft.Win32.SafeHandles;

namespace Codescene.VSExtension.Core.Util
{
    public static class RpcPath
    {
        private const uint FileGenericRead = 0x80000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint FileShareDelete = 0x00000004;
        private const uint OpenExisting = 3;
        private const uint FileFlagBackupSemantics = 0x02000000;
        private const uint FileNameNormalized = 0x0;

        private static readonly ConcurrentDictionary<string, string> CanonicalCache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, string> LocalRepoRoots = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

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

        public static void RememberLocalRepoRoot(string repoRoot)
        {
            if (string.IsNullOrEmpty(repoRoot))
            {
                return;
            }

            var canonical = CanonicalRepoRoot(repoRoot);
            LocalRepoRoots[canonical] = repoRoot;
        }

        public static string ToLocalRepoRoot(string reported)
        {
            if (string.IsNullOrEmpty(reported))
            {
                return reported ?? string.Empty;
            }

            var canonical = CanonicalRepoRoot(reported);
            return LocalRepoRoots.TryGetValue(canonical, out var local) ? local : reported;
        }

        public static string CanonicalRepoRoot(string repoRoot)
        {
            if (string.IsNullOrEmpty(repoRoot))
            {
                return NormalizeFsPath(repoRoot);
            }

            var lookupKey = NormalizeFsPath(repoRoot);
            if (CanonicalCache.TryGetValue(lookupKey, out var cached))
            {
                return cached;
            }

            var canonical = TryResolveCanonicalPath(repoRoot);
            if (canonical != null)
            {
                CanonicalCache[lookupKey] = canonical;
                return canonical;
            }

            return lookupKey;
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
            if (string.Equals(NormalizeFsPath(left), NormalizeFsPath(right), StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(CanonicalRepoRoot(left), CanonicalRepoRoot(right), StringComparison.Ordinal);
        }

        public static string CombineRepo(string repoRoot, string posixRelPath)
        {
            var relative = (posixRelPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(repoRoot ?? string.Empty, relative);
        }

        private static string TryResolveCanonicalPath(string repoRoot)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return null;
            }

            try
            {
                var fullPath = Path.GetFullPath(repoRoot);
                if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
                {
                    return null;
                }

                var openPath = ConvertExtendedPath(fullPath, toExtended: true);
                using var handle = CreateFileW(
                    openPath,
                    FileGenericRead,
                    FileShareRead | FileShareWrite | FileShareDelete,
                    IntPtr.Zero,
                    OpenExisting,
                    FileFlagBackupSemantics,
                    IntPtr.Zero);
                if (handle.IsInvalid)
                {
                    return null;
                }

                var buffer = new StringBuilder(512);
                var length = GetFinalPathNameByHandleW(handle, buffer, buffer.Capacity, FileNameNormalized);
                if (length == 0 || length >= buffer.Capacity)
                {
                    return null;
                }

                var resolved = ConvertExtendedPath(buffer.ToString(), toExtended: false);
                return NormalizeFsPath(resolved);
            }
            catch
            {
                return null;
            }
        }

        private static string ConvertExtendedPath(string path, bool toExtended)
        {
            if (toExtended)
            {
                if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
                {
                    return path;
                }

                if (path.StartsWith(@"\\", StringComparison.Ordinal))
                {
                    return @"\\?\UNC\" + path.Substring(2);
                }

                return @"\\?\" + path;
            }

            if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            {
                return @"\\" + path.Substring(7);
            }

            if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                return path.Substring(4);
            }

            return path;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandleW(SafeFileHandle hFile, StringBuilder lpszFilePath, int cchFilePath, uint dwFlags);
    }
}
