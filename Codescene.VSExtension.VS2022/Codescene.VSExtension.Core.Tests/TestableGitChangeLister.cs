// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.Core.Tests
{
    public class TestableGitChangeLister : GitChangeLister
    {
        public TestableGitChangeLister(
            ISavedFilesTracker savedFilesTracker,
            ISupportedFileChecker supportedFileChecker,
            ILogger logger,
            IGitService gitService)
            : base(savedFilesTracker, supportedFileChecker, logger, gitService)
        {
        }

        public bool ThrowInGetAllChangedFilesAsync { get; set; }

        public async Task InvokePeriodicScanAsync()
        {
            var method = typeof(GitChangeLister).GetMethod("PeriodicScanAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task)method.Invoke(this, null);
            await task;
        }

        public HashSet<string> InvokeConvertAndFilterPaths(IEnumerable<string> relativePaths, string gitRootPath)
        {
            return ConvertAndFilterPaths(relativePaths, gitRootPath);
        }

        public override async Task<HashSet<string>> GetAllChangedFilesAsync(string gitRootPath, string workspacePath)
        {
            if (ThrowInGetAllChangedFilesAsync)
            {
                throw new Exception("Simulated exception in CollectFilesFromRepoStateAsync");
            }

            return await base.GetAllChangedFilesAsync(gitRootPath, workspacePath);
        }
    }
}
