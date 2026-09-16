// Copyright (c) CodeScene. All rights reserved.

using System.Reflection;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Interfaces.Util;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using LibGit2Sharp;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliExecutorTests
    {
        private const string TestCachePath = "/test/cache/path";
        private const string TestFileContent = "public class Test { }";
        private static readonly string TestFilePath = $"{TestCachePath}/test.cs";

        private Mock<ILogger> _mockLogger;
        private Mock<IIdeServerClient> _mockClient;
        private Mock<ICacheStorageService> _mockCacheStorage;
        private Mock<ISettingsProvider> _mockSettingsProvider;
        private Mock<ITelemetryManager> _mockTelemetryManager;
        private Lazy<ITelemetryManager> _lazyTelemetryManager;
        private CliExecutor _cliExecutor;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockClient = new Mock<IIdeServerClient>();
            _mockCacheStorage = new Mock<ICacheStorageService>();
            _mockSettingsProvider = new Mock<ISettingsProvider>();
            _mockTelemetryManager = new Mock<ITelemetryManager>();
            _lazyTelemetryManager = new Lazy<ITelemetryManager>(() => _mockTelemetryManager.Object);
            _mockCacheStorage.Setup(x => x.GetSolutionReviewCacheLocation()).Returns(TestCachePath);
            _cliExecutor = CreateExecutor();
        }

        [TestMethod]
        public async Task ReviewContentAsync_WithValidResponse_ReturnsCliReviewModel()
        {
            var expectedReview = new CliReviewModel { Score = 7.5f, RawScore = "base64encoded" };
            _mockClient.Setup(x => x.ReviewAsync(It.IsAny<ReviewRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedReview);

            var result = await _cliExecutor.ReviewContentAsync(TestFilePath, TestFileContent);

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedReview.Score, result.Score);
            Assert.AreEqual(expectedReview.RawScore, result.RawScore);
            _mockClient.Verify(
                x => x.ReviewAsync(
                    It.Is<ReviewRequestModel>(r => r.FilePath == TestFilePath && r.FileContent == TestFileContent && r.CachePath == TestCachePath),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task ReviewContentAsync_FileInGitRepository_IncludesRepositoryRoot()
        {
            var repositoryRoot = Path.Combine(Path.GetTempPath(), "cli-review-repo-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repositoryRoot);
            Repository.Init(repositoryRoot);
            var filePath = Path.Combine(repositoryRoot, "test.cs");
            File.WriteAllText(filePath, TestFileContent);
            ReviewRequestModel request = null;
            _mockClient.Setup(x => x.ReviewAsync(It.IsAny<ReviewRequestModel>(), It.IsAny<CancellationToken>()))
                .Callback<ReviewRequestModel, CancellationToken>((value, _) => request = value)
                .ReturnsAsync(new CliReviewModel());

            try
            {
                await _cliExecutor.ReviewContentAsync(filePath, TestFileContent);

                var repoPathProperty = typeof(ReviewRequestModel).GetProperty("RepoPath");
                Assert.IsNotNull(repoPathProperty);
                Assert.AreEqual(
                    Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    repoPathProperty.GetValue(request));
            }
            finally
            {
                Directory.Delete(repositoryRoot, recursive: true);
            }
        }

        [TestMethod]
        public async Task ReviewContentAsync_WhenClientThrowsDevtoolsException_ThrowsException()
        {
            _mockClient.Setup(x => x.ReviewAsync(It.IsAny<ReviewRequestModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DevtoolsException("CLI error", 500, "trace-123"));

            var exception = await Assert.ThrowsAsync<DevtoolsException>(() =>
                _cliExecutor.ReviewContentAsync(TestFilePath, TestFileContent));
            Assert.AreEqual("CLI error", exception.Message);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Review of file")), It.IsAny<DevtoolsException>()), Times.Once);
            _mockLogger.Verify(x => x.Warn(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WhenClientThrowsRefactoringCreditsDevtoolsException_LogsWarningNotError()
        {
            const string creditsMessage =
                "Your credits of refactoring functionality ran out. Buy a bigger plan.";
            _mockClient.Setup(x => x.ReviewAsync(It.IsAny<ReviewRequestModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DevtoolsException(creditsMessage, 402, "trace-xyz"));

            var exception = await Assert.ThrowsAsync<DevtoolsException>(() =>
                _cliExecutor.ReviewContentAsync(TestFilePath, TestFileContent));
            Assert.AreEqual(creditsMessage, exception.Message);
            _mockLogger.Verify(
                x => x.Warn(It.Is<string>(s => s.Contains("Review of file") && s.Contains(creditsMessage) && s.Contains("402") && s.Contains("trace-xyz")), It.IsAny<bool>()),
                Times.Once);
            _mockLogger.Verify(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WhenClientThrowsGenericException_ReturnsNull()
        {
            _mockClient.Setup(x => x.ReviewAsync(It.IsAny<ReviewRequestModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Generic error"));
            var result = await _cliExecutor.ReviewContentAsync(TestFilePath, TestFileContent);

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Review of file")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WhenClientReturnsNull_ReturnsNull()
        {
            _mockClient.Setup(x => x.ReviewAsync(It.IsAny<ReviewRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((CliReviewModel)null);

            var result = await _cliExecutor.ReviewContentAsync(TestFilePath, TestFileContent);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task ReviewDeltaAsync_WithValidResponse_ReturnsDeltaResponseModel()
        {
            var expectedDelta = new DeltaResponseModel { NewScore = 8.5m, OldScore = 7.0m, ScoreChange = 1.5m };
            _mockClient.Setup(x => x.DeltaAsync("old-score-b64", "new-score-b64", It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedDelta);

            var result = await _cliExecutor.ReviewDeltaAsync(new ReviewDeltaRequest
            {
                OldScore = "old-score-b64",
                NewScore = "new-score-b64",
                FilePath = TestFilePath,
                FileContent = TestFileContent,
            });

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedDelta.NewScore, result.NewScore);
            Assert.AreEqual(expectedDelta.OldScore, result.OldScore);
            Assert.AreEqual(expectedDelta.ScoreChange, result.ScoreChange);
        }

        [TestMethod]
        public async Task ReviewDeltaAsync_WhenClientThrowsException_ReturnsNull()
        {
            _mockClient.Setup(x => x.DeltaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Error"));

            var result = await _cliExecutor.ReviewDeltaAsync(new ReviewDeltaRequest { OldScore = "old", NewScore = "new" });

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Delta for file failed")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task PreflightAsync_WithValidResponse_ReturnsPreFlightResponseModel()
        {
            var expectedPreflight = new PreFlightResponseModel { Version = 1.0m, FileTypes = new[] { ".cs", ".js" } };
            _mockClient.Setup(x => x.PreflightAsync(true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedPreflight);

            var result = await _cliExecutor.PreflightAsync(force: true);

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedPreflight.Version, result.Version);
            Assert.IsTrue(result.FileTypes.SequenceEqual(expectedPreflight.FileTypes));
        }

        [TestMethod]
        public async Task PreflightAsync_WhenClientThrowsException_ReturnsNull()
        {
            _mockClient.Setup(x => x.PreflightAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Error"));

            var result = await _cliExecutor.PreflightAsync();

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Preflight failed")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task PreflightAsync_WithForceFalse_UsesCorrectFlag()
        {
            var preflight = new PreFlightResponseModel { Version = 1.0m };
            _mockClient.Setup(x => x.PreflightAsync(false, It.IsAny<CancellationToken>()))
                .ReturnsAsync(preflight);

            var result = await _cliExecutor.PreflightAsync(force: false);

            Assert.IsNotNull(result);
            _mockClient.Verify(x => x.PreflightAsync(false, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CliExecutor(null, _mockClient.Object, _mockCacheStorage.Object, _mockSettingsProvider.Object));
        }

        [TestMethod]
        public void Constructor_WithNullClient_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CliExecutor(_mockLogger.Object, null, _mockCacheStorage.Object, _mockSettingsProvider.Object));
        }

        [TestMethod]
        public void Constructor_WithNullTelemetryManagerLazy_DoesNotThrow()
        {
            var executor = new CliExecutor(
                _mockLogger.Object,
                _mockClient.Object,
                _mockCacheStorage.Object,
                _mockSettingsProvider.Object);

            Assert.IsNotNull(executor);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WhenCancelled_ReturnsNull()
        {
            var completion = new TaskCompletionSource<CliReviewModel>();
            _mockClient.Setup(x => x.ReviewAsync(It.IsAny<ReviewRequestModel>(), It.IsAny<CancellationToken>()))
                .Returns<ReviewRequestModel, CancellationToken>((_, ct) =>
                {
                    ct.Register(() => completion.TrySetCanceled(ct));
                    return completion.Task;
                });

            var cts = new CancellationTokenSource();
            var task = _cliExecutor.ReviewContentAsync(TestFilePath, TestFileContent, false, cts.Token);
            cts.Cancel();

            var result = await task;

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WhenSecondCallCancelsFirst_FirstReturnsNull()
        {
            var firstCompletion = new TaskCompletionSource<CliReviewModel>();
            var callCount = 0;
            _mockClient.Setup(x => x.ReviewAsync(It.IsAny<ReviewRequestModel>(), It.IsAny<CancellationToken>()))
                .Returns<ReviewRequestModel, CancellationToken>((_, ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return firstCompletion.Task;
                    }

                    return Task.FromResult(new CliReviewModel { Score = 7.5f, RawScore = "raw" });
                });

            var firstTask = _cliExecutor.ReviewContentAsync(TestFilePath, TestFileContent, false);
            await Task.Delay(50);
            var secondTask = _cliExecutor.ReviewContentAsync(TestFilePath, TestFileContent, false);
            firstCompletion.SetCanceled();

            var firstResult = await firstTask;
            var secondResult = await secondTask;

            Assert.IsNull(firstResult);
            Assert.IsNotNull(secondResult);
        }

        [TestMethod]
        public async Task ReviewContentAsync_ConcurrentDifferentFiles_UsesBoundedConcurrency()
        {
            var firstFile = $"{TestCachePath}/first.cs";
            var secondFile = $"{TestCachePath}/second.cs";
            var completion = new TaskCompletionSource<bool>();
            var startedSignal = new TaskCompletionSource<bool>();
            var callCount = 0;

            _mockClient.Setup(x => x.ReviewAsync(It.IsAny<ReviewRequestModel>(), It.IsAny<CancellationToken>()))
                .Returns<ReviewRequestModel, CancellationToken>(async (request, _) =>
                {
                    Interlocked.Increment(ref callCount);
                    startedSignal.TrySetResult(true);
                    await completion.Task;
                    return new CliReviewModel { Score = 7.5f, RawScore = request.FilePath };
                });

            var firstTask = _cliExecutor.ReviewContentAsync(firstFile, TestFileContent);
            await startedSignal.Task;
            var secondTask = _cliExecutor.ReviewContentAsync(secondFile, TestFileContent);
            await Task.Delay(100);

            Assert.AreEqual(1, callCount, "Second review should wait for the shared CLI channel.");

            completion.TrySetResult(true);
            var firstResult = await firstTask;
            var secondResult = await secondTask;

            Assert.IsNotNull(firstResult);
            Assert.IsNotNull(secondResult);
            Assert.AreEqual(2, callCount);
        }

        [TestMethod]
        public void GetReviewCancellationPathIdentity_WhenGetFullPathThrows_ReturnsOriginalPath()
        {
            var method = typeof(CliExecutor).GetMethod("GetReviewCancellationPathIdentity", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            var tooLong = new string('a', 40000);
            var result = (string)method.Invoke(null, new object[] { tooLong });
            Assert.AreEqual(tooLong, result);
        }

        [TestMethod]
        public async Task ExecuteOnChannelAsync_AcquiresSemaphoreBeforeWaitingForCpu()
        {
            var operationLog = new List<string>();
            var semaphoreAcquiredSignal = new TaskCompletionSource<bool>();
            var cpuCheckSignal = new TaskCompletionSource<bool>();

            var mockThrottler = new Mock<ICpuUsageThrottler>();
            mockThrottler.Setup(x => x.WaitForCpuAsync(It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    lock (operationLog)
                    {
                        operationLog.Add("cpu_wait");
                    }

                    semaphoreAcquiredSignal.TrySetResult(true);
                    await cpuCheckSignal.Task;
                });

            var executor = CreateExecutor(mockThrottler.Object, 1);
            _mockClient.Setup(x => x.ReviewAsync(It.IsAny<ReviewRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CliReviewModel { Score = 1 });

            var task1 = executor.ReviewContentAsync("/file1.cs", "content1");
            var task2 = executor.ReviewContentAsync("/file2.cs", "content2");

            await semaphoreAcquiredSignal.Task;
            await Task.Delay(50);

            int cpuWaitCount;
            lock (operationLog)
            {
                cpuWaitCount = operationLog.Count(x => x == "cpu_wait");
            }

            Assert.AreEqual(
                1,
                cpuWaitCount,
                "Only the operation that acquired the semaphore should check CPU; the other should be blocked waiting for the semaphore");

            cpuCheckSignal.SetResult(true);
            await Task.WhenAll(task1, task2);
        }

        [TestMethod]
        public async Task ReviewDeltaAsync_AcquiresSemaphoreBeforeWaitingForCpu()
        {
            var operationLog = new List<string>();
            var semaphoreAcquiredSignal = new TaskCompletionSource<bool>();
            var cpuCheckSignal = new TaskCompletionSource<bool>();

            var mockThrottler = new Mock<ICpuUsageThrottler>();
            mockThrottler.Setup(x => x.WaitForCpuAsync(It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    lock (operationLog)
                    {
                        operationLog.Add("cpu_wait");
                    }

                    semaphoreAcquiredSignal.TrySetResult(true);
                    await cpuCheckSignal.Task;
                });

            var executor = CreateExecutor(mockThrottler.Object, 1);
            _mockClient.Setup(x => x.DeltaAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeltaResponseModel { NewScore = 8.5m, OldScore = 7.0m });

            var task1 = executor.ReviewDeltaAsync(new ReviewDeltaRequest { OldScore = "old1", NewScore = "new1", FilePath = "/file1.cs" });
            var task2 = executor.ReviewDeltaAsync(new ReviewDeltaRequest { OldScore = "old2", NewScore = "new2", FilePath = "/file2.cs" });

            await semaphoreAcquiredSignal.Task;
            await Task.Delay(50);

            int cpuWaitCount;
            lock (operationLog)
            {
                cpuWaitCount = operationLog.Count(x => x == "cpu_wait");
            }

            Assert.AreEqual(
                1,
                cpuWaitCount,
                "Only the operation that acquired the delta semaphore should check CPU; the other should be blocked waiting for the semaphore");

            cpuCheckSignal.SetResult(true);
            await Task.WhenAll(task1, task2);
        }

        private CliExecutor CreateExecutor(ICpuUsageThrottler throttler = null, int concurrency = 1)
        {
            return new CliExecutor(
                _mockLogger.Object,
                _mockClient.Object,
                _mockCacheStorage.Object,
                _mockSettingsProvider.Object,
                _lazyTelemetryManager,
                throttler,
                concurrency);
        }
    }
}
