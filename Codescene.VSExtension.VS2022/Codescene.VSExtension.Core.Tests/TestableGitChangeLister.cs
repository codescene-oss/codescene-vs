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
            ILogger logger)
            : base(savedFilesTracker, supportedFileChecker, logger)
        {
        }

        public bool ThrowInCollectFilesFromRepoStateAsync { get; set; }

        public async Task InvokePeriodicScanAsync()
        {
            var method = typeof(GitChangeLister).GetMethod("PeriodicScanAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task)method.Invoke(this, null);
            await task;
        }

        public override async Task<HashSet<string>> CollectFilesFromRepoStateAsync(string gitRootPath, string workspacePath)
        {
            if (ThrowInCollectFilesFromRepoStateAsync)
            {
                throw new Exception("Simulated exception in CollectFilesFromRepoStateAsync");
            }

            return await base.CollectFilesFromRepoStateAsync(gitRootPath, workspacePath);
        }
    }
}
