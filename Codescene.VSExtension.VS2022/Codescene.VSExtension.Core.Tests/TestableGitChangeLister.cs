// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Git;
using Codescene.VSExtension.Core.Interfaces.Util;

namespace Codescene.VSExtension.Core.Tests
{
    public class TestableGitChangeLister : GitChangeLister
    {
        public TestableGitChangeLister(
            ISavedFilesTracker savedFilesTracker,
            ISupportedFileChecker supportedFileChecker,
            ILogger logger,
            IGitService gitService,
            IIdeActivityTracker ideActivityTracker)
            : base(savedFilesTracker, supportedFileChecker, logger, gitService, ideActivityTracker: ideActivityTracker)
        {
        }

        public TestableGitChangeLister(
            ISavedFilesTracker savedFilesTracker,
            ISupportedFileChecker supportedFileChecker,
            ILogger logger,
            IGitService gitService,
            int? pollingInterval = null,
            IIdeActivityTracker ideActivityTracker = null,
            ICpuUsageChecker cpuUsageChecker = null)
            : base(savedFilesTracker, supportedFileChecker, logger, gitService, pollingInterval, ideActivityTracker, cpuUsageChecker)
        {
        }

        public bool ThrowInGetAllChangedFilesAsync { get; set; }

        public TimeSpan? SimulatedScanDuration { get; set; }

        public async Task InvokePeriodicScanAsync()
        {
            var method = typeof(GitChangeLister).GetMethod("PeriodicScanAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { typeof(CancellationToken) }, null);
            var task = (Task)method.Invoke(this, new object[] { CancellationToken.None });
            await task;
        }

        public HashSet<string> InvokeConvertAndFilterPaths(IEnumerable<string> relativePaths, string gitRootPath)
        {
            return ConvertAndFilterPaths(relativePaths, gitRootPath);
        }

        public override async Task<HashSet<string>> GetAllChangedFilesAsync(string gitRootPath, string workspacePath, CancellationToken cancellationToken = default)
        {
            if (ThrowInGetAllChangedFilesAsync)
            {
                throw new Exception("Simulated exception in CollectFilesFromRepoStateAsync");
            }

            return await base.GetAllChangedFilesAsync(gitRootPath, workspacePath, cancellationToken);
        }

        public override async Task<HashSet<string>> GetAllChangedFilesAsync(string gitRootPath, IReadOnlyCollection<string> workspacePaths, CancellationToken cancellationToken = default)
        {
            if (ThrowInGetAllChangedFilesAsync)
            {
                throw new Exception("Simulated exception in CollectFilesFromRepoStateAsync");
            }

            if (SimulatedScanDuration.HasValue)
            {
                await Task.Delay(SimulatedScanDuration.Value, cancellationToken);
            }

            return await base.GetAllChangedFilesAsync(gitRootPath, workspacePaths, cancellationToken);
        }

        public DroppingScheduledExecutor? GetScheduledExecutorForTesting()
        {
            var field = typeof(GitChangeLister).GetField("_scheduledExecutor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(this) as DroppingScheduledExecutor;
        }
    }
}
