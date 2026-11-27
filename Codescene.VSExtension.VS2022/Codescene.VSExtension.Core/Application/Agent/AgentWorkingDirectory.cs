// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Agent
{
    internal static class AgentWorkingDirectory
    {
        public static string ForFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var gitRoot = GitPathDiscovery.TryGetWorkingDirectory(path);
            if (!string.IsNullOrEmpty(gitRoot))
            {
                return gitRoot;
            }

            return Path.GetDirectoryName(path);
        }
    }
}
