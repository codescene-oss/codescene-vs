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
        private const string TestFileName = "test.cs";
        private const string TestFileContent = "public class Test { }";

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
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFileName, TestFileContent, TestCachePath))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(jsonResponse);

            var result = await _cliExecutor.ReviewContentAsync(TestFileName, TestFileContent);

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedReview.Score, result.Score);
            Assert.AreEqual(expectedReview.RawScore, result.RawScore);
            _mockProcessExecutor.Verify(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<System.Threading.CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WhenProcessExecutorThrowsDevtoolsException_ThrowsException()
        {
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFileName, TestFileContent, TestCachePath))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<System.Threading.CancellationToken>()))
                .ThrowsAsync(new DevtoolsException("CLI error", 500, "trace-123"));

            var exception = await Assert.ThrowsAsync<DevtoolsException>(() =>
                _cliExecutor.ReviewContentAsync(TestFileName, TestFileContent));
            Assert.AreEqual("CLI error", exception.Message);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Review of file")), It.IsAny<DevtoolsException>()), Times.Once);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WhenProcessExecutorThrowsGenericException_ReturnsNull()
        {
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFileName, TestFileContent, TestCachePath))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Generic error"));
            var result = await _cliExecutor.ReviewContentAsync(TestFileName, TestFileContent);

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Review of file")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WithInvalidJson_ReturnsNull()
        {
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFileName, TestFileContent, TestCachePath))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync("invalid json");
            var result = await _cliExecutor.ReviewContentAsync(TestFileName, TestFileContent);

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
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(jsonResponse);

            var result = await _cliExecutor.ReviewDeltaAsync(new ReviewDeltaRequest { OldScore = oldScore, NewScore = newScore, FilePath = TestFileName, FileContent = TestFileContent });

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
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<System.Threading.CancellationToken>()))
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
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<System.Threading.CancellationToken>()))
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
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<System.Threading.CancellationToken>()))
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
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<System.Threading.CancellationToken>()))
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
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFileName, TestFileContent, TestCachePath))
                .Returns("payload");
            var completion = new TaskCompletionSource<string>();
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<CancellationToken>()))
                .Returns(completion.Task);

            var cts = new CancellationTokenSource();
            var task = _cliExecutor.ReviewContentAsync(TestFileName, TestFileContent, false, cts.Token);
            cts.Cancel();

            var result = await task;

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task ReviewContentAsync_WhenSecondCallCancelsFirst_FirstReturnsNull()
        {
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFileName, TestFileContent, TestCachePath))
                .Returns("payload");
            var firstCompletion = new TaskCompletionSource<string>();
            var callCount = 0;
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("review --file-name test.cs", "payload", null, It.IsAny<CancellationToken>()))
                .Returns<string, string, TimeSpan?, CancellationToken>((cmd, payload, timeout, ct) =>
                {
                    callCount++;
                    if (callCount == 1)
                    {
                        return firstCompletion.Task;
                    }

                    return Task.FromResult(JsonConvert.SerializeObject(new CliReviewModel { Score = 7.5f, RawScore = "raw" }));
                });

            var firstTask = _cliExecutor.ReviewContentAsync(TestFileName, TestFileContent, false);
            await Task.Delay(50);
            var secondTask = _cliExecutor.ReviewContentAsync(TestFileName, TestFileContent, false);
            firstCompletion.SetCanceled();

            var firstResult = await firstTask;
            var secondResult = await secondTask;

            Assert.IsNull(firstResult);
            Assert.IsNotNull(secondResult);
        }
    }
}
