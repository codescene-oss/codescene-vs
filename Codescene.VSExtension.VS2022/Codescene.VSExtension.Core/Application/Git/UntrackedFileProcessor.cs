// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Codescene.VSExtension.Core.Application.Git
{
    internal class UntrackedFileProcessor
    {
        private const int MaxUntrackedFilesPerLocation = 5;

        public void AddUntrackedFileToDirectory(
            string relativePath,
            string absolutePath,
            Dictionary<string, List<string>> untrackedByDirectory)
        {
            var directory = string.IsNullOrEmpty(Path.GetDirectoryName(relativePath)) ? "__root__" : Path.GetDirectoryName(relativePath);

            if (!untrackedByDirectory.ContainsKey(directory))
            {
                untrackedByDirectory[directory] = new List<string>();
            }

            untrackedByDirectory[directory].Add(absolutePath);
        }

        public void ProcessUntrackedDirectories(
            Dictionary<string, List<string>> untrackedByDirectory,
            HashSet<string> savedFiles,
            HashSet<string> changedFiles)
        {
            foreach (var kvp in untrackedByDirectory)
            {
                var files = kvp.Value;
                var filesToAdd = files.Count > MaxUntrackedFilesPerLocation
                    ? files.Where(f => savedFiles.Contains(f))
                    : files;

                changedFiles.UnionWith(filesToAdd);
            }
        }
    }
}
