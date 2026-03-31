// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Concurrent;
using System.Collections.Generic;
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
            _deltaCache = new DeltaCacheService(new ConcurrentDictionary<string, DeltaCacheItem>());
        }

        [TestMethod]
        public async Task FileCreation_WithoutDeletion_ShowsInCodeHealthMonitor()
        {
            var aboutToStore = new ManualResetEventSlim(false);
            var canProceed = new ManualResetEventSlim(true);
            var blockingReviewer = new BlockingCodeReviewer(aboutToStore, canProceed, _deltaCache);

            var observer = new GitChangeObserverCore(
                _fakeLogger, blockingReviewer, _fakeSupportedFileChecker, _fakeTaskScheduler, _fakeGitChangeLister, _fakeGitService);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver, null);

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
                _fakeLogger, blockingReviewer, _fakeSupportedFileChecker, _fakeTaskScheduler, _fakeGitChangeLister, _fakeGitService);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver, null);

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

        [TestMethod]
        public async Task CancelAndReset_WhileReviewInFlight_PreventsCachePut()
        {
            var aboutToStore = new ManualResetEventSlim(false);
            var canProceed = new ManualResetEventSlim(false);
            var deltaCache = new DeltaCacheService(new ConcurrentDictionary<string, DeltaCacheItem>());
            var blockingReviewer = new CancellationAwareBlockingCodeReviewer(aboutToStore, canProceed, deltaCache);
            var backgroundScheduler = new BackgroundAsyncTaskScheduler();

            var observer = new GitChangeObserverCore(
                _fakeLogger, blockingReviewer, _fakeSupportedFileChecker, backgroundScheduler, _fakeGitChangeLister, _fakeGitService);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver, null);
            observer.Start();

            var testFile = CreateFile("test.cs", "public class Test {}");
            _fakeGitChangeLister.SimulateFilesDetected(new HashSet<string> { testFile });

            var waitResult = aboutToStore.Wait(5000);
            Assert.IsTrue(waitResult, "Review should reach the point before Put() within timeout");

            observer.CancelAndReset();

            canProceed.Set();
            await Task.Delay(200);

            var filesInCache = deltaCache.GetAll();
            Assert.IsFalse(
                filesInCache.ContainsKey(testFile),
                "Cancelled in-flight review should NOT populate the cache");

            observer.Dispose();
        }

        [TestMethod]
        public async Task FilesDetected_WhileReviewInFlight_DoesNotRunParallelReviewForSameFile()
        {
            var reviewStarted = new ManualResetEventSlim(false);
            var unblockReview = new ManualResetEventSlim(false);
            var countingReviewer = new ParallelBlockingCodeReviewer(reviewStarted, unblockReview);
            var backgroundScheduler = new BackgroundAsyncTaskScheduler();

            var observer = new GitChangeObserverCore(
                _fakeLogger, countingReviewer, _fakeSupportedFileChecker, backgroundScheduler, _fakeGitChangeLister, _fakeGitService);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver, null);
            observer.Start();

            var testFile = CreateFile("same-file.cs", "public class SameFile {}");
            var detected = new HashSet<string> { testFile };
            _fakeGitChangeLister.SimulateFilesDetected(detected);

            var waitResult = reviewStarted.Wait(5000);
            Assert.IsTrue(waitResult, "Review should start.");

            _fakeGitChangeLister.SimulateFilesDetected(detected);
            await Task.Delay(200);

            Assert.AreEqual(1, countingReviewer.StartedCount, "Second scan should not start parallel work for same file while first run is active.");
            Assert.AreEqual(1, countingReviewer.MaxParallelism, "Only one in-flight review should be active for the file.");

            unblockReview.Set();
            await Task.Delay(300);

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

            public Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new FileReviewModel { FilePath = path, RawScore = "8.5" });
            }

            public Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                _aboutToStore.Set();
                _canProceed.Wait();

                var delta = new DeltaResponseModel { ScoreChange = -0.5m };
                var entry = new DeltaCacheEntry(review.FilePath, string.Empty, currentCode, delta);
                _cache.Put(entry);

                return Task.FromResult(delta);
            }

            public async Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                var review = await ReviewAsync(path, currentCode, false, operationGeneration, cancellationToken);
                var baselineRawScore = await GetOrComputeBaselineRawScoreAsync(path, string.Empty, operationGeneration, cancellationToken);
                return (review, baselineRawScore ?? string.Empty);
            }

            public async Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsync(string path, string content, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                var (review, baselineRawScore) = await ReviewAndBaselineAsync(path, content, operationGeneration, cancellationToken);
                var delta = await DeltaAsync(review, content, baselineRawScore, operationGeneration, cancellationToken);
                return (review, delta);
            }

            public Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult("8.0");
            }
        }

        private class CancellationAwareBlockingCodeReviewer : ICodeReviewer
        {
            private readonly ManualResetEventSlim _aboutToStore;
            private readonly ManualResetEventSlim _canProceed;
            private readonly DeltaCacheService _cache;

            public CancellationAwareBlockingCodeReviewer(
                ManualResetEventSlim aboutToStore, ManualResetEventSlim canProceed, DeltaCacheService cache)
            {
                _aboutToStore = aboutToStore;
                _canProceed = canProceed;
                _cache = cache;
            }

            public Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new FileReviewModel { FilePath = path, RawScore = "8.5" });
            }

            public Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                _aboutToStore.Set();
                _canProceed.Wait();

                cancellationToken.ThrowIfCancellationRequested();

                var delta = new DeltaResponseModel { ScoreChange = -0.5m };
                var entry = new DeltaCacheEntry(review.FilePath, string.Empty, currentCode, delta);
                _cache.Put(entry);

                return Task.FromResult(delta);
            }

            public async Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                var review = await ReviewAsync(path, currentCode, false, operationGeneration, cancellationToken);
                var baselineRawScore = await GetOrComputeBaselineRawScoreAsync(path, string.Empty, operationGeneration, cancellationToken);
                return (review, baselineRawScore ?? string.Empty);
            }

            public async Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsync(string path, string content, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                var (review, baselineRawScore) = await ReviewAndBaselineAsync(path, content, operationGeneration, cancellationToken);
                var delta = await DeltaAsync(review, content, baselineRawScore, operationGeneration, cancellationToken);
                return (review, delta);
            }

            public Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult("8.0");
            }
        }

        private class ParallelBlockingCodeReviewer : ICodeReviewer
        {
            private readonly ManualResetEventSlim _reviewStarted;
            private readonly ManualResetEventSlim _unblockReview;
            private int _activeCount;

            public ParallelBlockingCodeReviewer(ManualResetEventSlim reviewStarted, ManualResetEventSlim unblockReview)
            {
                _reviewStarted = reviewStarted;
                _unblockReview = unblockReview;
            }

            public int StartedCount { get; private set; }

            public int MaxParallelism { get; private set; }

            public Task<FileReviewModel> ReviewAsync(string path, string content, bool isBaseline = false, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new FileReviewModel { FilePath = path, RawScore = "8.5" });
            }

            public Task<DeltaResponseModel> DeltaAsync(FileReviewModel review, string currentCode, string precomputedBaselineRawScore = null, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                StartedCount++;
                var currentActive = Interlocked.Increment(ref _activeCount);
                if (currentActive > MaxParallelism)
                {
                    MaxParallelism = currentActive;
                }

                _reviewStarted.Set();
                _unblockReview.Wait();

                Interlocked.Decrement(ref _activeCount);
                return Task.FromResult<DeltaResponseModel>(null);
            }

            public async Task<(FileReviewModel review, string baselineRawScore)> ReviewAndBaselineAsync(string path, string currentCode, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                var review = await ReviewAsync(path, currentCode, false, operationGeneration, cancellationToken);
                var baselineRawScore = await GetOrComputeBaselineRawScoreAsync(path, string.Empty, operationGeneration, cancellationToken);
                return (review, baselineRawScore ?? string.Empty);
            }

            public async Task<(FileReviewModel review, DeltaResponseModel delta)> ReviewWithDeltaAsync(string path, string content, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                var (review, baselineRawScore) = await ReviewAndBaselineAsync(path, content, operationGeneration, cancellationToken);
                var delta = await DeltaAsync(review, content, baselineRawScore, operationGeneration, cancellationToken);
                return (review, delta);
            }

            public Task<string> GetOrComputeBaselineRawScoreAsync(string path, string baselineContent, long? operationGeneration = null, CancellationToken cancellationToken = default)
            {
                return Task.FromResult("8.0");
            }
        }
    }
}
