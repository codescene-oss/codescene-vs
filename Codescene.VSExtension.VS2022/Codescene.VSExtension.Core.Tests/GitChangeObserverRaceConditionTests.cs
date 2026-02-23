// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cache.Delta;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeObserverRaceConditionTests : GitChangeObserverTestBase
    {
        private DeltaCacheService _deltaCache;

        [TestInitialize]
        public void SetupRaceConditionTests()
        {
            _deltaCache = new DeltaCacheService();
            _deltaCache.Clear();
        }

        [TestMethod]
        public async Task FileCreation_WithoutDeletion_ShowsInCodeHealthMonitor()
        {
            var aboutToStore = new ManualResetEventSlim(false);
            var canProceed = new ManualResetEventSlim(true);
            var blockingReviewer = new BlockingCodeReviewer(aboutToStore, canProceed, _deltaCache);

            var observer = new GitChangeObserverCore(
                _fakeLogger, blockingReviewer, _fakeSupportedFileChecker, _fakeTaskScheduler, _fakeGitChangeLister);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            var testFile = CreateFile("test.cs", "public class Test {}");
            var changedFiles = await observer.GetChangedFilesVsBaselineAsync();

            await observer.HandleFileChangeForTestingAsync(testFile, changedFiles);

            await WaitForConditionAsync(() => aboutToStore.IsSet, 5000);

            var filesInMonitor = _deltaCache.GetAll();
            Assert.IsTrue(
                filesInMonitor.ContainsKey(testFile),
                "Created file should appear in Code Health Monitor");

            observer.Dispose();
        }

        [TestMethod]
        public async Task FileCreatedThenDeletedDuringReview_ShouldNotShowInCodeHealthMonitor()
        {
            var aboutToStore = new ManualResetEventSlim(false);
            var canProceed = new ManualResetEventSlim(false);
            var blockingReviewer = new BlockingCodeReviewer(aboutToStore, canProceed, _deltaCache);

            var observer = new GitChangeObserverCore(
                _fakeLogger, blockingReviewer, _fakeSupportedFileChecker, _fakeTaskScheduler, _fakeGitChangeLister);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            var testFile = CreateFile("test.cs", "public class Test {}");
            var changedFiles = await observer.GetChangedFilesVsBaselineAsync();

            var changeTask = Task.Run(async () =>
                await observer.HandleFileChangeForTestingAsync(testFile, changedFiles));

            var waitResult = aboutToStore.Wait(5000);
            Assert.IsTrue(waitResult, "Should reach the point before Put() within timeout");

            File.Delete(testFile);
            await observer.HandleFileDeleteForTestingAsync(testFile, changedFiles);

            canProceed.Set();
            await changeTask;

            var filesInMonitor = _deltaCache.GetAll();
            Assert.IsFalse(
                filesInMonitor.ContainsKey(testFile),
                "Deleted file should NOT appear in Code Health Monitor");

            observer.Dispose();
        }

        private class BlockingCodeReviewer : ICodeReviewer
        {
            private readonly ManualResetEventSlim _aboutToStore;
            private readonly ManualResetEventSlim _canProceed;
            private readonly DeltaCacheService _cache;

            public BlockingCodeReviewer(
                ManualResetEventSlim aboutToStore, ManualResetEventSlim canProceed, DeltaCacheService cache)
            {
                _aboutToStore = aboutToStore;
                _canProceed = canProceed;
                _cache = cache;
            }

            public Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new FileReviewModel { FilePath = path, RawScore = "8.5" });
            }

            public Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, CancellationToken cancellationToken = default)
            {
                _aboutToStore.Set();
                _canProceed.Wait();

                var delta = new DeltaResponseModel { ScoreChange = -0.5m };
                var entry = new DeltaCacheEntry(review.FilePath, string.Empty, currentCode, delta);
                _cache.Put(entry);

                return Task.FromResult(delta);
            }

            public async Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, CancellationToken cancellationToken = default)
            {
                var review = await ReviewAsync(path, currentCode, false, cancellationToken);
                var baselineRawScore = await GetOrComputeBaselineRawScoreAsync(path, string.Empty, cancellationToken);
                return (review, baselineRawScore ?? string.Empty);
            }

            public Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, CancellationToken cancellationToken = default)
            {
                return Task.FromResult("8.0");
            }
        }
    }
}
