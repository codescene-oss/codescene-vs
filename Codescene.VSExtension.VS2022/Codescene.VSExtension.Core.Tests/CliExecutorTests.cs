using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliExecutorTests
    {
        private Mock<ILogger> _mockLogger;
        private Mock<ICliCommandProvider> _mockCommandProvider;
        private Mock<IProcessExecutor> _mockProcessExecutor;
        private Mock<ISettingsProvider> _mockSettingsProvider;
        private Mock<ICacheStorageService> _mockCacheStorageService;
        private CliExecutor _executor;

        private const string TestFilename = "test.cs";
        private const string TestContent = "code";
        private const string TestCachePath = "/cache";

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockCommandProvider = new Mock<ICliCommandProvider>();
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _mockSettingsProvider = new Mock<ISettingsProvider>();
            _mockCacheStorageService = new Mock<ICacheStorageService>();

            _executor = new CliExecutor(
                _mockLogger.Object,
                _mockCommandProvider.Object,
                _mockProcessExecutor.Object,
                _mockSettingsProvider.Object,
                _mockCacheStorageService.Object);
        }

        private void SetupReviewContentMocks(string cachePath = TestCachePath)
        {
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns("{}");
            _mockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation()).Returns(cachePath);
        }

        private void SetupFnsToRefactorMocks(string payload = "payload", string response = "[]")
        {
            _mockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation()).Returns(TestCachePath);
            _mockCommandProvider.Setup(x => x.RefactorCommand).Returns("command");
            _mockCommandProvider.Setup(x => x.GetRefactorWithCodeSmellsPayload(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IList<CliCodeSmellModel>>(), It.IsAny<PreFlightResponseModel>())).Returns(payload);
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), null)).Returns(response);
        }

        [TestMethod]
        public void ReviewContent_ValidResponse_ReturnsDeserializedModel()
        {
            var cachePath = "/cache/path";
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("run-command review");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFilename, "public class Test {}", cachePath)).Returns("{}");
            _mockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation()).Returns(cachePath);
            _mockProcessExecutor.Setup(x => x.Execute("run-command review", "{}", null)).Returns("{\"score\": 8.5, \"raw-score\": \"abc123\"}");

            var result = _executor.ReviewContent(TestFilename, "public class Test {}");

            Assert.IsNotNull(result);
            Assert.AreEqual(8.5f, result.Score);
        }

        [TestMethod]
        public void ReviewContent_ProcessThrowsException_ReturnsNull()
        {
            SetupReviewContentMocks();
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), null)).Throws(new Exception("CLI error"));

            var result = _executor.ReviewContent(TestFilename, TestContent);

            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public void ReviewContent_DevtoolsException_RethrowsException()
        {
            SetupReviewContentMocks();
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), null)).Throws(new DevtoolsException("Devtools error", 10, "traceId"));

            Assert.Throws<DevtoolsException>(() => _executor.ReviewContent(TestFilename, TestContent));
        }

        [TestMethod]
        public void ReviewDelta_EmptyArguments_ReturnsNull()
        {
            // Arrange
            _mockCommandProvider.Setup(x => x.GetReviewDeltaCommand(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(string.Empty);

            // Act
            var result = _executor.ReviewDelta("old", "new");

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Skipping delta review"))), Times.Once);
        }

        [TestMethod]
        public void ReviewDelta_ValidResponse_ReturnsDeserializedModel()
        {
            // Arrange
            var arguments = "{\"old-score\":\"abc\",\"new-score\":\"def\"}";
            var jsonResponse = "{\"score-change\": -0.5, \"old-score\": 8.0, \"new-score\": 7.5}";

            _mockCommandProvider.Setup(x => x.GetReviewDeltaCommand("old", "new")).Returns(arguments);
            _mockProcessExecutor.Setup(x => x.Execute("delta", arguments, null)).Returns(jsonResponse);

            // Act
            var result = _executor.ReviewDelta("old", "new");

            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void Preflight_EmptyArguments_ReturnsNull()
        {
            // Arrange
            _mockCommandProvider.Setup(x => x.GetPreflightSupportInformationCommand(true))
                .Returns(string.Empty);

            // Act
            var result = _executor.Preflight(true);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Skipping preflight"))), Times.Once);
        }

        [TestMethod]
        public void Preflight_ValidResponse_ReturnsDeserializedModel()
        {
            // Arrange
            var command = "refactor preflight --force";
            var jsonResponse = "{\"file-types\": [\".cs\", \".js\"]}";

            _mockCommandProvider.Setup(x => x.GetPreflightSupportInformationCommand(true)).Returns(command);
            _mockProcessExecutor.Setup(x => x.Execute(command, null, It.IsAny<TimeSpan?>())).Returns(jsonResponse);

            // Act
            var result = _executor.Preflight(true);

            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void PostRefactoring_EmptyArguments_ReturnsNull()
        {
            // Arrange
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns("token");
            _mockCommandProvider.Setup(x => x.GetRefactorPostCommand(fnToRefactor, false, "token"))
                .Returns(string.Empty);

            // Act
            var result = _executor.PostRefactoring(fnToRefactor, false);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Skipping refactoring"))), Times.Once);
        }

        [TestMethod]
        public void PostRefactoring_MissingAuthToken_ThrowsMissingAuthTokenException()
        {
            // Arrange
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns(string.Empty);

            // Act & Assert
            Assert.Throws<MissingAuthTokenException>(() => _executor.PostRefactoring(fnToRefactor, false));
        }

        [TestMethod]
        public void PostRefactoring_NullAuthToken_ThrowsMissingAuthTokenException()
        {
            // Arrange
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns((string)null);

            // Act & Assert
            Assert.Throws<MissingAuthTokenException>(() => _executor.PostRefactoring(fnToRefactor, false));
        }

        [TestMethod]
        public void PostRefactoring_ValidResponse_ReturnsDeserializedModel()
        {
            // Arrange
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            var command = "refactor post --fn-to-refactor {}";
            var jsonResponse = "{\"trace-id\": \"123\", \"refactored-code\": \"new code\"}";

            _mockSettingsProvider.Setup(x => x.AuthToken).Returns("token");
            _mockCommandProvider.Setup(x => x.GetRefactorPostCommand(fnToRefactor, false, "token")).Returns(command);
            _mockProcessExecutor.Setup(x => x.Execute(command, null, null)).Returns(jsonResponse);

            // Act
            var result = _executor.PostRefactoring(fnToRefactor, false);

            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void FnsToRefactorFromCodeSmells_EmptyPayload_ReturnsNull()
        {
            SetupFnsToRefactorMocks(payload: string.Empty);

            var result = _executor.FnsToRefactorFromCodeSmells(TestFilename, TestContent, new List<CliCodeSmellModel>(), new PreFlightResponseModel());

            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Payload content was not defined"))), Times.Once);
        }

        [TestMethod]
        public void FnsToRefactorFromCodeSmells_ValidResponse_ReturnsDeserializedList()
        {
            SetupFnsToRefactorMocks(payload: "{}", response: "[{\"name\": \"TestFunction\", \"body\": \"code\"}]");

            var result = _executor.FnsToRefactorFromCodeSmells(TestFilename, TestContent,
                new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "Test" } },
                new PreFlightResponseModel());

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public void FnsToRefactorFromCodeSmells_RemovesOldCacheEntries()
        {
            SetupFnsToRefactorMocks();

            _executor.FnsToRefactorFromCodeSmells(TestFilename, TestContent, new List<CliCodeSmellModel>(), new PreFlightResponseModel());

            _mockCacheStorageService.Verify(x => x.RemoveOldReviewCacheEntries(It.IsAny<int>()), Times.Once);
        }

        [TestMethod]
        public void GetFileVersion_ValidResponse_ReturnsVersion()
        {
            _mockCommandProvider.Setup(x => x.VersionCommand).Returns("version --sha");
            _mockProcessExecutor.Setup(x => x.Execute("version --sha", null, null)).Returns("abc123def456\r\n");

            var result = _executor.GetFileVersion();

            Assert.AreEqual("abc123def456", result);
        }

        [TestMethod]
        public void GetFileVersion_ExceptionThrown_ReturnsEmptyString()
        {
            _mockCommandProvider.Setup(x => x.VersionCommand).Returns("version");
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), null, null)).Throws(new Exception("Error"));

            var result = _executor.GetFileVersion();

            Assert.AreEqual("", result);
            _mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public void GetDeviceId_ValidResponse_ReturnsDeviceId()
        {
            _mockCommandProvider.Setup(x => x.DeviceIdCommand).Returns("telemetry --device-id");
            _mockProcessExecutor.Setup(x => x.Execute("telemetry --device-id", null, null)).Returns("device-123\n");

            var result = _executor.GetDeviceId();

            Assert.AreEqual("device-123", result);
        }
    }
}
