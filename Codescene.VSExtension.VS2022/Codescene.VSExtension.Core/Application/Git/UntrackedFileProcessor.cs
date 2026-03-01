// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.Core.Application.Git
{
    internal class UntrackedFileProcessor
    {
        private const int MaxUntrackedFilesPerLocation = 5;
        private readonly ILogger _logger;
        private readonly IGitService _gitService;

        public UntrackedFileProcessor(IGitService gitService, ILogger logger = null)
        {
            _gitService = gitService;
            _logger = logger;
        }

        public void AddUntrackedFileToDirectory(
            string relativePath,
            string absolutePath,
            Dictionary<string, List<string>> untrackedByDirectory)
        {
            if (_gitService.IsFileIgnored(absolutePath))
            {
                return;
            }

            var directory = string.IsNullOrEmpty(Path.GetDirectoryName(relativePath)) ? "__root__" : Path.GetDirectoryName(relativePath);

            if (!untrackedByDirectory.ContainsKey(directory))
            {
                untrackedByDirectory[directory] = new List<string>();
            }

            untrackedByDirectory[directory].Add(absolutePath);
            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> UntrackedFileProcessor: Added untracked file '{absolutePath}' to directory tracking for '{directory}'");
            #endif
        }

        public void ProcessUntrackedDirectories(
            Dictionary<string, List<string>> untrackedByDirectory,
            HashSet<string> savedFiles,
            HashSet<string> changedFiles)
        {
            #if FEATURE_INITIAL_GIT_OBSERVER
            _logger?.Info($">>> UntrackedFileProcessor: Processing {untrackedByDirectory.Count} directories with untracked files");
            #endif
            foreach (var kvp in untrackedByDirectory)
            {
                var directory = kvp.Key;
                var files = kvp.Value;

                if (files.Count > MaxUntrackedFilesPerLocation)
                {
                    #if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> UntrackedFileProcessor: Directory '{directory}' exceeds threshold with {files.Count} untracked files (max: {MaxUntrackedFilesPerLocation})");
                    #endif
                }

                var filesToAdd = files.Count > MaxUntrackedFilesPerLocation
                    ? files.Where(f => savedFiles.Contains(f))
                    : files;

                var addedCount = filesToAdd.Count();
                if (addedCount > 0)
                {
                    #if FEATURE_INITIAL_GIT_OBSERVER
                    _logger?.Info($">>> UntrackedFileProcessor: Adding {addedCount} files from directory '{directory}'");
                    #endif
                }

                changedFiles.UnionWith(filesToAdd);
            }
        }
    }
}
