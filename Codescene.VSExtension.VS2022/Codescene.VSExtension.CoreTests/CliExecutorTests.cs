using Codescene.VSExtension.Core.Application.Services.Cache;
using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Settings;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;

namespace Codescene.VSExtension.CoreTests
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

        #region ReviewContent Tests

        [TestMethod]
        public void ReviewContent_ValidResponse_ReturnsDeserializedModel()
        {
            // Arrange
            var filename = "test.cs";
            var content = "public class Test {}";
            var cachePath = "/cache/path";
            var jsonResponse = "{\"score\": 8.5, \"raw-score\": \"abc123\"}";

            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("run-command review");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(filename, content, cachePath)).Returns("{}");
            _mockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation()).Returns(cachePath);
            _mockProcessExecutor.Setup(x => x.Execute("run-command review", "{}", null)).Returns(jsonResponse);

            // Act
            var result = _executor.ReviewContent(filename, content);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(8.5f, result.Score);
            Assert.AreEqual("abc123", result.RawScore);
        }

        [TestMethod]
        public void ReviewContent_ProcessThrowsException_ReturnsNull()
        {
            // Arrange
            var filename = "test.cs";
            var content = "code";
            var cachePath = "/cache";

            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns("{}");
            _mockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation()).Returns(cachePath);
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), null))
                .Throws(new Exception("CLI error"));

            // Act
            var result = _executor.ReviewContent(filename, content);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public void ReviewContent_DevtoolsException_RethrowsException()
        {
            // Arrange
            var filename = "test.cs";
            var content = "code";
            var cachePath = "/cache";

            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns("{}");
            _mockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation()).Returns(cachePath);
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), null))
                .Throws(new DevtoolsException("Devtools error", 10, "traceId"));

            // Act & Assert
            Assert.Throws<DevtoolsException>(() => _executor.ReviewContent(filename, content));
        }

        #endregion

        #region ReviewDelta Tests

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

        #endregion

        #region Preflight Tests

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

        #endregion

        #region PostRefactoring Tests

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

        #endregion

        #region FnsToRefactorFromCodeSmells Tests

        [TestMethod]
        public void FnsToRefactorFromCodeSmells_EmptyPayload_ReturnsNull()
        {
            // Arrange
            var codeSmells = new List<CliCodeSmellModel>();
            var preflight = new PreFlightResponseModel();

            _mockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation()).Returns("/cache");
            _mockCommandProvider.Setup(x => x.RefactorCommand).Returns("run-command fns-to-refactor");
            _mockCommandProvider.Setup(x => x.GetRefactorWithCodeSmellsPayload(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IList<CliCodeSmellModel>>(), It.IsAny<PreFlightResponseModel>()))
                .Returns(string.Empty);

            // Act
            var result = _executor.FnsToRefactorFromCodeSmells("test.cs", "code", codeSmells, preflight);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Payload content was not defined"))), Times.Once);
        }

        [TestMethod]
        public void FnsToRefactorFromCodeSmells_ValidResponse_ReturnsDeserializedList()
        {
            // Arrange
            var codeSmells = new List<CliCodeSmellModel>
            {
                new CliCodeSmellModel { Category = "Test" }
            };
            var preflight = new PreFlightResponseModel();
            var command = "run-command fns-to-refactor";
            var payload = "{\"code-smells\":[]}";
            var jsonResponse = "[{\"name\": \"TestFunction\", \"body\": \"code\"}]";

            _mockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation()).Returns("/cache");
            _mockCommandProvider.Setup(x => x.RefactorCommand).Returns(command);
            _mockCommandProvider.Setup(x => x.GetRefactorWithCodeSmellsPayload(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IList<CliCodeSmellModel>>(), It.IsAny<PreFlightResponseModel>()))
                .Returns(payload);
            _mockProcessExecutor.Setup(x => x.Execute(command, payload, null)).Returns(jsonResponse);

            // Act
            var result = _executor.FnsToRefactorFromCodeSmells("test.cs", "code", codeSmells, preflight);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("TestFunction", result[0].Name);
        }

        [TestMethod]
        public void FnsToRefactorFromCodeSmells_RemovesOldCacheEntries()
        {
            // Arrange
            var codeSmells = new List<CliCodeSmellModel>();
            var preflight = new PreFlightResponseModel();

            _mockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation()).Returns("/cache");
            _mockCommandProvider.Setup(x => x.RefactorCommand).Returns("command");
            _mockCommandProvider.Setup(x => x.GetRefactorWithCodeSmellsPayload(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IList<CliCodeSmellModel>>(), It.IsAny<PreFlightResponseModel>()))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), null)).Returns("[]");

            // Act
            _executor.FnsToRefactorFromCodeSmells("test.cs", "code", codeSmells, preflight);

            // Assert
            _mockCacheStorageService.Verify(x => x.RemoveOldReviewCacheEntries(It.IsAny<int>()), Times.Once);
        }

        #endregion

        #region GetFileVersion Tests

        [TestMethod]
        public void GetFileVersion_ValidResponse_ReturnsVersion()
        {
            // Arrange
            var versionCommand = "version --sha";
            var versionResponse = "abc123def456\r\n";

            _mockCommandProvider.Setup(x => x.VersionCommand).Returns(versionCommand);
            _mockProcessExecutor.Setup(x => x.Execute(versionCommand, null, null)).Returns(versionResponse);

            // Act
            var result = _executor.GetFileVersion();

            // Assert
            Assert.AreEqual("abc123def456", result);
        }

        [TestMethod]
        public void GetFileVersion_ExceptionThrown_ReturnsEmptyString()
        {
            // Arrange
            _mockCommandProvider.Setup(x => x.VersionCommand).Returns("version");
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), null, null))
                .Throws(new Exception("Error"));

            // Act
            var result = _executor.GetFileVersion();

            // Assert
            Assert.AreEqual("", result);
            _mockLogger.Verify(l => l.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
        }

        #endregion

        #region GetDeviceId Tests

        [TestMethod]
        public void GetDeviceId_ValidResponse_ReturnsDeviceId()
        {
            // Arrange
            var command = "telemetry --device-id";
            var response = "device-123\n";

            _mockCommandProvider.Setup(x => x.DeviceIdCommand).Returns(command);
            _mockProcessExecutor.Setup(x => x.Execute(command, null, null)).Returns(response);

            // Act
            var result = _executor.GetDeviceId();

            // Assert
            Assert.AreEqual("device-123", result);
        }

        #endregion
    }
}
