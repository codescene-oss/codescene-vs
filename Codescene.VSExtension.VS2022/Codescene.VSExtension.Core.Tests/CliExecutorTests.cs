// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Moq;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliExecutorTests
    {
        private const string TestCachePath = "/test/cache/path";
        private const string TestFileContent = "public class Test { }";
        private static readonly string TestFilePath = $"{TestCachePath}/test.cs";

        private Mock<ILogger> _mockLogger;
        private Mock<ICliServices> _mockCliServices;
        private Mock<ICliCommandProvider> _mockCommandProvider;
        private Mock<IProcessExecutor> _mockProcessExecutor;
        private Mock<ICacheStorageService> _mockCacheStorage;
        private Mock<ISettingsProvider> _mockSettingsProvider;
        private Mock<ITelemetryManager> _mockTelemetryManager;
        private Lazy<ITelemetryManager> _lazyTelemetryManager;
        private CliExecutor _cliExecutor;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockCommandProvider = new Mock<ICliCommandProvider>();
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _mockCacheStorage = new Mock<ICacheStorageService>();
            _mockSettingsProvider = new Mock<ISettingsProvider>();
            _mockTelemetryManager = new Mock<ITelemetryManager>();
            _lazyTelemetryManager = new Lazy<ITelemetryManager>(() => _mockTelemetryManager.Object);

            _mockCliServices = new Mock<ICliServices>();
            _mockCliServices.Setup(x => x.CommandProvider).Returns(_mockCommandProvider.Object);
            _mockCliServices.Setup(x => x.ProcessExecutor).Returns(_mockProcessExecutor.Object);
            _mockCliServices.Setup(x => x.CacheStorage).Returns(_mockCacheStorage.Object);

            _mockCacheStorage.Setup(x => x.GetSolutionReviewCacheLocation()).Returns(TestCachePath);
            _mockCacheStorage.Setup(x => x.GetWorkspaceDirectory()).Returns(string.Empty);

            _cliExecutor = new CliExecutor(
                _mockLogger.Object,
                _mockCliServices.Object,
                _mockSettingsProvider.Object,
                _lazyTelemetryManager);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WithValidResponse_ReturnsCliReviewModel()
        {
            var expectedReview = new CliReviewModel
            {
                Score = 7.5f,
                RawScore = "base64encoded",
            };
            var jsonResponse = JsonConvert.SerializeObject(expectedReview);
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFilePath, TestFileContent, TestCachePath))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(jsonResponse);

            var result = await _cliExecutor.ReviewContentAsync(TestFilePath, TestFileContent);

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedReview.Score, result.Score);
            Assert.AreEqual(expectedReview.RawScore, result.RawScore);
            _mockProcessExecutor.Verify(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<CancellationToken>(), It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WhenProcessExecutorThrowsDevtoolsException_ThrowsException()
        {
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFilePath, TestFileContent, TestCachePath))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ThrowsAsync(new DevtoolsException("CLI error", 500, "trace-123"));

            var exception = await Assert.ThrowsAsync<DevtoolsException>(() =>
                _cliExecutor.ReviewContentAsync(TestFilePath, TestFileContent));
            Assert.AreEqual("CLI error", exception.Message);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Review of file")), It.IsAny<DevtoolsException>()), Times.Once);
            _mockLogger.Verify(x => x.Warn(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WhenProcessExecutorThrowsRefactoringCreditsDevtoolsException_LogsWarningNotError()
        {
            const string creditsMessage =
                "Your credits of refactoring functionality ran out. Buy a bigger plan.";
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFilePath, TestFileContent, TestCachePath))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<CancellationToken>(), It.IsAny<string>()))
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
        public async Task ReviewContentAsync_WhenProcessExecutorThrowsGenericException_ReturnsNull()
        {
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFilePath, TestFileContent, TestCachePath))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Generic error"));
            var result = await _cliExecutor.ReviewContentAsync(TestFilePath, TestFileContent);

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Review of file")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WithInvalidJson_ReturnsNull()
        {
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFilePath, TestFileContent, TestCachePath))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync("invalid json");
            var result = await _cliExecutor.ReviewContentAsync(TestFilePath, TestFileContent);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task ReviewDeltaAsync_WithValidResponse_ReturnsDeltaResponseModel()
        {
            var oldScore = "old-score-b64";
            var newScore = "new-score-b64";
            var expectedDelta = new DeltaResponseModel
            {
                NewScore = 8.5m,
                OldScore = 7.0m,
                ScoreChange = 1.5m,
            };
            var jsonResponse = JsonConvert.SerializeObject(expectedDelta);
            _mockCommandProvider.Setup(x => x.GetReviewDeltaCommand(oldScore, newScore))
                .Returns("delta command");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(jsonResponse);

            var result = await _cliExecutor.ReviewDeltaAsync(new ReviewDeltaRequest { OldScore = oldScore, NewScore = newScore, FilePath = TestFilePath, FileContent = TestFileContent });

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedDelta.NewScore, result.NewScore);
            Assert.AreEqual(expectedDelta.OldScore, result.OldScore);
            Assert.AreEqual(expectedDelta.ScoreChange, result.ScoreChange);
        }

        [TestMethod]
        public async Task ReviewDeltaAsync_WithEmptyArguments_ReturnsNull()
        {
            _mockCommandProvider.Setup(x => x.GetReviewDeltaCommand(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(string.Empty);

            var result = await _cliExecutor.ReviewDeltaAsync(new ReviewDeltaRequest { OldScore = "old", NewScore = "new" });

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Warn("Skipping delta review. Arguments were not defined."), Times.Once);
        }

        [TestMethod]
        public async Task ReviewDeltaAsync_WithNullArguments_ReturnsNull()
        {
            _mockCommandProvider.Setup(x => x.GetReviewDeltaCommand(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string)null);

            var result = await _cliExecutor.ReviewDeltaAsync(new ReviewDeltaRequest { OldScore = "old", NewScore = "new" });

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Warn("Skipping delta review. Arguments were not defined."), Times.Once);
        }

        [TestMethod]
        public async Task ReviewDeltaAsync_WhenProcessExecutorThrowsException_ReturnsNull()
        {
            _mockCommandProvider.Setup(x => x.GetReviewDeltaCommand(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("delta command");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Error"));

            var result = await _cliExecutor.ReviewDeltaAsync(new ReviewDeltaRequest { OldScore = "old", NewScore = "new" });

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Delta for file failed")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task PreflightAsync_WithValidResponse_ReturnsPreFlightResponseModel()
        {
            var expectedPreflight = new PreFlightResponseModel
            {
                Version = 1.0m,
                FileTypes = new[] { ".cs", ".js" },
            };
            var jsonResponse = JsonConvert.SerializeObject(expectedPreflight);
            _mockCommandProvider.Setup(x => x.GetPreflightSupportInformationCommand(It.IsAny<bool>()))
                .Returns("refactor preflight --force");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(jsonResponse);

            var result = await _cliExecutor.PreflightAsync(force: true);

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedPreflight.Version, result.Version);
            Assert.IsTrue(result.FileTypes.SequenceEqual(expectedPreflight.FileTypes));
        }

        [TestMethod]
        public async Task PreflightAsync_WithEmptyArguments_ReturnsNull()
        {
            _mockCommandProvider.Setup(x => x.GetPreflightSupportInformationCommand(It.IsAny<bool>()))
                .Returns(string.Empty);

            var result = await _cliExecutor.PreflightAsync();

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Warn("Skipping preflight. Arguments were not defined."), Times.Once);
        }

        [TestMethod]
        public async Task PreflightAsync_WhenProcessExecutorThrowsException_ReturnsNull()
        {
            _mockCommandProvider.Setup(x => x.GetPreflightSupportInformationCommand(It.IsAny<bool>()))
                .Returns("refactor preflight");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Error"));

            var result = await _cliExecutor.PreflightAsync();

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Preflight failed")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task PreflightAsync_WithForceFalse_UsesCorrectCommand()
        {
            var preflight = new PreFlightResponseModel { Version = 1.0m };
            var jsonResponse = JsonConvert.SerializeObject(preflight);
            _mockCommandProvider.Setup(x => x.GetPreflightSupportInformationCommand(false))
                .Returns("refactor preflight");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .ReturnsAsync(jsonResponse);

            var result = await _cliExecutor.PreflightAsync(force: false);

            Assert.IsNotNull(result);
            _mockCommandProvider.Verify(x => x.GetPreflightSupportInformationCommand(false), Times.Once);
        }

        [TestMethod]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CliExecutor(null, _mockCliServices.Object, _mockSettingsProvider.Object));
        }

        [TestMethod]
        public void Constructor_WithNullCliServices_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CliExecutor(_mockLogger.Object, null, _mockSettingsProvider.Object));
        }

        [TestMethod]
        public void Constructor_WithNullTelemetryManagerLazy_DoesNotThrow()
        {
            var executor = new CliExecutor(
                _mockLogger.Object,
                _mockCliServices.Object,
                _mockSettingsProvider.Object);

            Assert.IsNotNull(executor);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WhenCancelled_ReturnsNull()
        {
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFilePath, TestFileContent, TestCachePath))
                .Returns("payload");
            var completion = new TaskCompletionSource<string>();
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .Returns<string, string, TimeSpan?, CancellationToken, string>((_, _, _, ct, __) =>
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
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFilePath, TestFileContent, TestCachePath))
                .Returns("payload");
            var firstCompletion = new TaskCompletionSource<string>();
            var callCount = 0;
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .Returns<string, string, TimeSpan?, CancellationToken, string>((cmd, payload, timeout, ct, _) =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return firstCompletion.Task;
                    }

                    return Task.FromResult(JsonConvert.SerializeObject(new CliReviewModel { Score = 7.5f, RawScore = "raw" }));
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

            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(It.IsAny<string>(), It.IsAny<string>(), TestCachePath))
                .Returns((string filePath, string _, string _) => "payload-" + filePath);
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name", It.IsAny<string>(), null, It.IsAny<CancellationToken>(), It.IsAny<string>()))
                .Returns<string, string, TimeSpan?, CancellationToken, string>(async (_, payload, _, _, __) =>
                {
                    Interlocked.Increment(ref callCount);
                    startedSignal.TrySetResult(true);
                    await completion.Task;
                    return JsonConvert.SerializeObject(new CliReviewModel { Score = 7.5f, RawScore = payload });
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
    }
}
