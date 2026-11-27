// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public static class AuthRepoPicker
    {
        public static AuthRepo Pick(IReadOnlyList<string> repoRoots, string activeFile)
        {
            if (repoRoots == null || repoRoots.Count == 0)
            {
                return null;
            }

            var outermostRoots = GetOutermostRoots(repoRoots);

            if (outermostRoots.Count == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(activeFile))
            {
                foreach (var root in outermostRoots)
                {
                    if (IsFileInsideRoot(activeFile, root))
                    {
                        return CreateRepoFromRoot(root);
                    }
                }
            }

            return CreateRepoFromRoot(outermostRoots[0]);
        }

        private static List<string> GetOutermostRoots(IReadOnlyList<string> roots)
        {
            var normalizedToOriginal = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                var normalizedPath = RpcPath.NormalizeFsPath(root);
                if (!normalizedToOriginal.ContainsKey(normalizedPath))
                {
                    normalizedToOriginal[normalizedPath] = root;
                }
            }

            var normalized = normalizedToOriginal.Keys.ToList();

            return normalized
                .Where(root => !normalized.Any(other => root != other && IsRootNestedInOther(root, other)))
                .Select(root => normalizedToOriginal[root])
                .ToList();
        }

        private static bool IsRootNestedInOther(string root, string other)
        {
            if (root.Length <= other.Length)
            {
                return false;
            }

            if (!root.StartsWith(other, StringComparison.Ordinal))
            {
                return false;
            }

            var nextChar = root[other.Length];
            return nextChar == '/' || nextChar == '\\';
        }

        private static bool IsFileInsideRoot(string activeFile, string root)
        {
            if (string.IsNullOrWhiteSpace(activeFile) || string.IsNullOrWhiteSpace(root))
            {
                return false;
            }

            var normalizedFile = RpcPath.NormalizeFsPath(activeFile);
            var normalizedRoot = RpcPath.NormalizeFsPath(root);

            if (normalizedFile.Length <= normalizedRoot.Length)
            {
                return false;
            }

            if (!normalizedFile.StartsWith(normalizedRoot, StringComparison.Ordinal))
            {
                return false;
            }

            var nextChar = normalizedFile[normalizedRoot.Length];
            return nextChar == '/' || nextChar == '\\';
        }

        private static AuthRepo CreateRepoFromRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                return null;
            }

            var trimmedRoot = root.TrimEnd('\\', '/');
            var name = Path.GetFileName(trimmedRoot);

            return new AuthRepo(root, name ?? string.Empty);
        }
    }
}
