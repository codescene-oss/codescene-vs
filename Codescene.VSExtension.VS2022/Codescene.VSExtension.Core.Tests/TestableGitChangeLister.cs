// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
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

        public int GetAllChangedFilesCallCount { get; private set; }

        public int CollectFilesFromRepoStateCallCount { get; private set; }

        public bool ThrowInGetRepoStateAsync { get; set; }

        public void ResetCallCounts()
        {
            GetAllChangedFilesCallCount = 0;
            CollectFilesFromRepoStateCallCount = 0;
        }

        public async Task InvokePeriodicScanAsync()
        {
            var method = typeof(GitChangeLister).GetMethod("PeriodicScanAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task)method.Invoke(this, null);
            await task;
        }

        public HashSet<string> InvokeConvertAndFilterPaths(IEnumerable<string> relativePaths, string gitRootPath)
        {
            return ConvertAndFilterPaths(relativePaths, gitRootPath);
        }

        public override async Task<HashSet<string>> GetAllChangedFilesAsync(string gitRootPath, string workspacePath)
        {
            GetAllChangedFilesCallCount++;
            if (ThrowInCollectFilesFromRepoStateAsync)
            {
                throw new Exception("Simulated exception in CollectFilesFromRepoStateAsync");
            }

            return await base.GetAllChangedFilesAsync(gitRootPath, workspacePath);
        }

        public override async Task<HashSet<string>> CollectFilesFromRepoStateAsync(string gitRootPath, string workspacePath)
        {
            CollectFilesFromRepoStateCallCount++;
            if (ThrowInCollectFilesFromRepoStateAsync)
            {
                throw new Exception("Simulated exception in CollectFilesFromRepoStateAsync");
            }

            return await base.CollectFilesFromRepoStateAsync(gitRootPath, workspacePath);
        }

        protected override async Task<RepoState> GetRepoStateAsync(string gitRootPath)
        {
            if (ThrowInGetRepoStateAsync)
            {
                throw new InvalidOperationException("Simulated GetRepoStateAsync failure");
            }

            return await base.GetRepoStateAsync(gitRootPath);
        }
    }
}
