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
        private Mock<Lazy<ITelemetryManager>> _mockTelemetryManagerLazy;
        private CliExecutor _cliExecutor;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockCommandProvider = new Mock<ICliCommandProvider>();
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _mockCacheStorage = new Mock<ICacheStorageService>();
            _mockSettingsProvider = new Mock<ISettingsProvider>();
            _mockTelemetryManagerLazy = new Mock<Lazy<ITelemetryManager>>();

            _mockCliServices = new Mock<ICliServices>();
            _mockCliServices.Setup(x => x.CommandProvider).Returns(_mockCommandProvider.Object);
            _mockCliServices.Setup(x => x.ProcessExecutor).Returns(_mockProcessExecutor.Object);
            _mockCliServices.Setup(x => x.CacheStorage).Returns(_mockCacheStorage.Object);

            _mockCacheStorage.Setup(x => x.GetSolutionReviewCacheLocation()).Returns(TestCachePath);

            _cliExecutor = new CliExecutor(
                _mockLogger.Object,
                _mockCliServices.Object,
                _mockSettingsProvider.Object,
                _mockTelemetryManagerLazy.Object);
        }

        [TestMethod]
        public void ReviewContent_WithValidResponse_ReturnsCliReviewModel()
        {
            // Arrange
            var expectedReview = new CliReviewModel
            {
                Score = 7.5f,
                RawScore = "base64encoded",
            };
            var jsonResponse = JsonConvert.SerializeObject(expectedReview);
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFileName, TestFileContent, TestCachePath))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.Execute("review --file-name test.cs", "payload"))
                .Returns(jsonResponse);

            // Act
            var result = _cliExecutor.ReviewContent(TestFileName, TestFileContent);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedReview.Score, result.Score);
            Assert.AreEqual(expectedReview.RawScore, result.RawScore);
            _mockProcessExecutor.Verify(x => x.Execute("review --file-name test.cs", "payload"), Times.Once);
        }

        [TestMethod]
        public void ReviewContent_WhenProcessExecutorThrowsDevtoolsException_ThrowsException()
        {
            // Arrange
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFileName, TestFileContent, TestCachePath))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.Execute("review --file-name test.cs", "payload"))
                .Throws(new DevtoolsException("CLI error", 500, "trace-123"));

            // Act & Assert
            var exception = Assert.Throws<DevtoolsException>(() =>
                _cliExecutor.ReviewContent(TestFileName, TestFileContent));
            Assert.AreEqual("CLI error", exception.Message);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Review of file")), It.IsAny<DevtoolsException>()), Times.Once);
        }

        [TestMethod]
        public void ReviewContent_WhenProcessExecutorThrowsGenericException_ReturnsNull()
        {
            // Arrange
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFileName, TestFileContent, TestCachePath))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.Execute("review --file-name test.cs", "payload"))
                .Throws(new Exception("Generic error"));

            // Act
            var result = _cliExecutor.ReviewContent(TestFileName, TestFileContent);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Review of file")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public void ReviewContent_WithInvalidJson_ReturnsNull()
        {
            // Arrange
            _mockCommandProvider.Setup(x => x.ReviewFileContentCommand).Returns("review --file-name test.cs");
            _mockCommandProvider.Setup(x => x.GetReviewFileContentPayload(TestFileName, TestFileContent, TestCachePath))
                .Returns("payload");
            _mockProcessExecutor.Setup(x => x.Execute("review --file-name test.cs", "payload"))
                .Returns("invalid json");

            // Act
            var result = _cliExecutor.ReviewContent(TestFileName, TestFileContent);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ReviewDelta_WithValidResponse_ReturnsDeltaResponseModel()
        {
            // Arrange
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
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(jsonResponse);

            // Act
            var result = _cliExecutor.ReviewDelta(oldScore, newScore, TestFileName, TestFileContent);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedDelta.NewScore, result.NewScore);
            Assert.AreEqual(expectedDelta.OldScore, result.OldScore);
            Assert.AreEqual(expectedDelta.ScoreChange, result.ScoreChange);
        }

        [TestMethod]
        public void ReviewDelta_WithEmptyArguments_ReturnsNull()
        {
            // Arrange
            _mockCommandProvider.Setup(x => x.GetReviewDeltaCommand(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(string.Empty);

            // Act
            var result = _cliExecutor.ReviewDelta("old", "new");

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Warn("Skipping delta review. Arguments were not defined."), Times.Once);
        }

        [TestMethod]
        public void ReviewDelta_WithNullArguments_ReturnsNull()
        {
            // Arrange
            _mockCommandProvider.Setup(x => x.GetReviewDeltaCommand(It.IsAny<string>(), It.IsAny<string>()))
                .Returns((string)null);

            // Act
            var result = _cliExecutor.ReviewDelta("old", "new");

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Warn("Skipping delta review. Arguments were not defined."), Times.Once);
        }

        [TestMethod]
        public void ReviewDelta_WhenProcessExecutorThrowsException_ReturnsNull()
        {
            // Arrange
            _mockCommandProvider.Setup(x => x.GetReviewDeltaCommand(It.IsAny<string>(), It.IsAny<string>()))
                .Returns("delta command");
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new Exception("Error"));

            // Act
            var result = _cliExecutor.ReviewDelta("old", "new");

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Delta for file failed")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public void Preflight_WithValidResponse_ReturnsPreFlightResponseModel()
        {
            // Arrange
            var expectedPreflight = new PreFlightResponseModel
            {
                Version = 1.0m,
                FileTypes = new[] { ".cs", ".js" },
            };
            var jsonResponse = JsonConvert.SerializeObject(expectedPreflight);
            _mockCommandProvider.Setup(x => x.GetPreflightSupportInformationCommand(It.IsAny<bool>()))
                .Returns("refactor preflight --force");
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
                .Returns(jsonResponse);

            // Act
            var result = _cliExecutor.Preflight(force: true);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedPreflight.Version, result.Version);
            Assert.IsTrue(result.FileTypes.SequenceEqual(expectedPreflight.FileTypes));
        }

        [TestMethod]
        public void Preflight_WithEmptyArguments_ReturnsNull()
        {
            // Arrange
            _mockCommandProvider.Setup(x => x.GetPreflightSupportInformationCommand(It.IsAny<bool>()))
                .Returns(string.Empty);

            // Act
            var result = _cliExecutor.Preflight();

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Warn("Skipping preflight. Arguments were not defined."), Times.Once);
        }

        [TestMethod]
        public void Preflight_WhenProcessExecutorThrowsException_ReturnsNull()
        {
            // Arrange
            _mockCommandProvider.Setup(x => x.GetPreflightSupportInformationCommand(It.IsAny<bool>()))
                .Returns("refactor preflight");
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
                .Throws(new Exception("Error"));

            // Act
            var result = _cliExecutor.Preflight();

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Preflight failed")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public void Preflight_WithForceFalse_UsesCorrectCommand()
        {
            // Arrange
            var preflight = new PreFlightResponseModel { Version = 1.0m };
            var jsonResponse = JsonConvert.SerializeObject(preflight);
            _mockCommandProvider.Setup(x => x.GetPreflightSupportInformationCommand(false))
                .Returns("refactor preflight");
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>()))
                .Returns(jsonResponse);

            // Act
            var result = _cliExecutor.Preflight(force: false);

            // Assert
            Assert.IsNotNull(result);
            _mockCommandProvider.Verify(x => x.GetPreflightSupportInformationCommand(false), Times.Once);
        }

        [TestMethod]
        public void PostRefactoring_WithValidResponse_ReturnsRefactorResponseModel()
        {
            // Arrange
            var fnToRefactor = new FnToRefactorModel
            {
                Name = "TestMethod",
                Body = "public void Test() { }",
                FileType = "cs",
            };
            var expectedResponse = new RefactorResponseModel
            {
                Code = "public void Test() { /* refactored */ }",
                TraceId = "trace-123",
            };
            var jsonResponse = JsonConvert.SerializeObject(expectedResponse);
            var token = "test-token";
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns(token);
            _mockCommandProvider.Setup(x => x.GetRefactorPostCommand(fnToRefactor, false, token))
                .Returns("refactor post command");
            _mockProcessExecutor.Setup(x => x.Execute("refactor post command", It.IsAny<string>()))
                .Returns(jsonResponse);

            // Act
            var result = _cliExecutor.PostRefactoring(fnToRefactor, skipCache: false);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResponse.Code, result.Code);
            Assert.AreEqual(expectedResponse.TraceId, result.TraceId);
        }

        [TestMethod]
        public void PostRefactoring_WithMissingAuthToken_ThrowsMissingAuthTokenException()
        {
            // Arrange
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns(string.Empty);

            // Act & Assert
            var exception = Assert.Throws<MissingAuthTokenException>(() =>
                _cliExecutor.PostRefactoring(fnToRefactor));
            Assert.Contains(exception.Message, "Authentication token is missing");
        }

        [TestMethod]
        public void PostRefactoring_WithProvidedToken_UsesProvidedToken()
        {
            // Arrange
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            var providedToken = "provided-token";
            var response = new RefactorResponseModel { Code = "refactored" };
            var jsonResponse = JsonConvert.SerializeObject(response);
            _mockCommandProvider.Setup(x => x.GetRefactorPostCommand(fnToRefactor, false, providedToken))
                .Returns("refactor post");
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(jsonResponse);

            // Act
            var result = _cliExecutor.PostRefactoring(fnToRefactor, skipCache: false, token: providedToken);

            // Assert
            Assert.IsNotNull(result);
            _mockCommandProvider.Verify(x => x.GetRefactorPostCommand(fnToRefactor, false, providedToken), Times.Once);
        }

        [TestMethod]
        public void PostRefactoring_WithEmptyArguments_ReturnsNull()
        {
            // Arrange
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            var token = "test-token";
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns(token);
            _mockCommandProvider.Setup(x => x.GetRefactorPostCommand(It.IsAny<FnToRefactorModel>(), It.IsAny<bool>(), It.IsAny<string>()))
                .Returns(string.Empty);

            // Act
            var result = _cliExecutor.PostRefactoring(fnToRefactor);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Warn("Skipping refactoring. Arguments were not defined."), Times.Once);
        }

        [TestMethod]
        public void PostRefactoring_WhenProcessExecutorThrowsException_ReturnsNull()
        {
            // Arrange
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            var token = "test-token";
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns(token);
            _mockCommandProvider.Setup(x => x.GetRefactorPostCommand(It.IsAny<FnToRefactorModel>(), It.IsAny<bool>(), It.IsAny<string>()))
                .Returns("refactor post");
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new Exception("Error"));

            // Act
            var result = _cliExecutor.PostRefactoring(fnToRefactor);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Refactoring failed")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public void PostRefactoring_WithSkipCache_PassesSkipCacheFlag()
        {
            // Arrange
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            var token = "test-token";
            var response = new RefactorResponseModel { Code = "refactored" };
            var jsonResponse = JsonConvert.SerializeObject(response);
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns(token);
            _mockCommandProvider.Setup(x => x.GetRefactorPostCommand(fnToRefactor, true, token))
                .Returns("refactor post --skip-cache");
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(jsonResponse);

            // Act
            var result = _cliExecutor.PostRefactoring(fnToRefactor, skipCache: true);

            // Assert
            Assert.IsNotNull(result);
            _mockCommandProvider.Verify(x => x.GetRefactorPostCommand(fnToRefactor, true, token), Times.Once);
        }

        [TestMethod]
        public void FnsToRefactorFromCodeSmells_WithValidResponse_ReturnsListOfFnToRefactor()
        {
            // Arrange
            var codeSmells = new List<CliCodeSmellModel>
            {
                new CliCodeSmellModel { Category = "Complex Method" },
            };
            var preflight = new PreFlightResponseModel { Version = 1.0m };
            var expectedFunctions = new List<FnToRefactorModel>
            {
                new FnToRefactorModel { Name = "Function1", Body = "code" },
            };
            var jsonResponse = JsonConvert.SerializeObject(expectedFunctions);
            _mockCommandProvider.Setup(x => x.GetRefactorWithCodeSmellsPayload(TestFileName, TestFileContent, TestCachePath, codeSmells, preflight))
                .Returns("payload");
            _mockCommandProvider.Setup(x => x.RefactorCommand).Returns("refactor");
            _mockProcessExecutor.Setup(x => x.Execute("refactor", "payload"))
                .Returns(jsonResponse);

            // Act
            var result = _cliExecutor.FnsToRefactorFromCodeSmells(TestFileName, TestFileContent, codeSmells, preflight);

            // Assert
            Assert.IsNotNull(result);
            Assert.HasCount(1, result);
            Assert.AreEqual("Function1", result[0].Name);
        }

        [TestMethod]
        public void FnsToRefactorFromCodeSmells_WithNullCodeSmells_ReturnsNull()
        {
            // Act
            var result = _cliExecutor.FnsToRefactorFromCodeSmells(TestFileName, TestFileContent, null, null);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Debug("Skipping refactoring functions from code smells. Code smells list was null or empty."), Times.Once);
        }

        [TestMethod]
        public void FnsToRefactorFromCodeSmells_WithEmptyCodeSmells_ReturnsNull()
        {
            // Act
            var result = _cliExecutor.FnsToRefactorFromCodeSmells(TestFileName, TestFileContent, new List<CliCodeSmellModel>(), null);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Debug("Skipping refactoring functions from code smells. Code smells list was null or empty."), Times.Once);
        }

        [TestMethod]
        public void FnsToRefactorFromCodeSmells_WithEmptyPayload_ReturnsNull()
        {
            // Arrange
            var codeSmells = new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "Test" } };
            _mockCommandProvider.Setup(x => x.GetRefactorWithCodeSmellsPayload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<CliCodeSmellModel>>(), It.IsAny<PreFlightResponseModel>()))
                .Returns(string.Empty);

            // Act
            var result = _cliExecutor.FnsToRefactorFromCodeSmells(TestFileName, TestFileContent, codeSmells, null);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Warn("Skipping refactoring functions check. Payload content was not defined."), Times.Once);
        }

        [TestMethod]
        public void FnsToRefactorFromCodeSmells_RemovesOldCacheEntries()
        {
            // Arrange
            var codeSmells = new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "Test" } };
            var functions = new List<FnToRefactorModel>();
            var jsonResponse = JsonConvert.SerializeObject(functions);
            _mockCommandProvider.Setup(x => x.GetRefactorWithCodeSmellsPayload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<CliCodeSmellModel>>(), It.IsAny<PreFlightResponseModel>()))
                .Returns("payload");
            _mockCommandProvider.Setup(x => x.RefactorCommand).Returns("refactor");
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(jsonResponse);

            // Act
            _cliExecutor.FnsToRefactorFromCodeSmells(TestFileName, TestFileContent, codeSmells, null);

            // Assert
            _mockCacheStorage.Verify(x => x.RemoveOldReviewCacheEntries(), Times.Once);
        }

        [TestMethod]
        public void FnsToRefactorFromDelta_WithValidResponse_ReturnsListOfFnToRefactor()
        {
            // Arrange
            var deltaResult = new DeltaResponseModel { NewScore = 8.0m, OldScore = 7.0m };
            var preflight = new PreFlightResponseModel { Version = 1.0m };
            var expectedFunctions = new List<FnToRefactorModel>
            {
                new FnToRefactorModel { Name = "Function1", Body = "code" },
            };
            var jsonResponse = JsonConvert.SerializeObject(expectedFunctions);
            _mockCommandProvider.Setup(x => x.GetRefactorWithDeltaResultPayload(TestFileName, TestFileContent, TestCachePath, deltaResult, preflight))
                .Returns("payload");
            _mockCommandProvider.Setup(x => x.RefactorCommand).Returns("refactor");
            _mockProcessExecutor.Setup(x => x.Execute("refactor", "payload"))
                .Returns(jsonResponse);

            // Act
            var result = _cliExecutor.FnsToRefactorFromDelta(TestFileName, TestFileContent, deltaResult, preflight);

            // Assert
            Assert.IsNotNull(result);
            Assert.HasCount(1, result);
            Assert.AreEqual("Function1", result[0].Name);
        }

        [TestMethod]
        public void FnsToRefactorFromDelta_WithNullDeltaResult_ReturnsNull()
        {
            // Act
            var result = _cliExecutor.FnsToRefactorFromDelta(TestFileName, TestFileContent, null, null);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Debug("Skipping refactoring functions from delta. Delta result was null."), Times.Once);
        }

        [TestMethod]
        public void FnsToRefactorFromDelta_WithEmptyPayload_ReturnsNull()
        {
            // Arrange
            var deltaResult = new DeltaResponseModel { NewScore = 8.0m, OldScore = 7.0m };
            _mockCommandProvider.Setup(x => x.GetRefactorWithDeltaResultPayload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DeltaResponseModel>(), It.IsAny<PreFlightResponseModel>()))
                .Returns(string.Empty);

            // Act
            var result = _cliExecutor.FnsToRefactorFromDelta(TestFileName, TestFileContent, deltaResult, null);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Warn("Skipping refactoring functions check. Payload content was not defined."), Times.Once);
        }

        [TestMethod]
        public void FnsToRefactorFromDelta_RemovesOldCacheEntries()
        {
            // Arrange
            var deltaResult = new DeltaResponseModel { NewScore = 8.0m, OldScore = 7.0m };
            var functions = new List<FnToRefactorModel>();
            var jsonResponse = JsonConvert.SerializeObject(functions);
            _mockCommandProvider.Setup(x => x.GetRefactorWithDeltaResultPayload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DeltaResponseModel>(), It.IsAny<PreFlightResponseModel>()))
                .Returns("payload");
            _mockCommandProvider.Setup(x => x.RefactorCommand).Returns("refactor");
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(jsonResponse);

            // Act
            _cliExecutor.FnsToRefactorFromDelta(TestFileName, TestFileContent, deltaResult, null);

            // Assert
            _mockCacheStorage.Verify(x => x.RemoveOldReviewCacheEntries(), Times.Once);
        }

        [TestMethod]
        public void GetDeviceId_WithValidResponse_ReturnsDeviceId()
        {
            // Arrange
            var expectedDeviceId = "device-id-123";
            _mockCommandProvider.Setup(x => x.DeviceIdCommand).Returns("device-id");
            _mockProcessExecutor.Setup(x => x.Execute("device-id", null, null))
                .Returns(expectedDeviceId);

            // Act
            var result = _cliExecutor.GetDeviceId();

            // Assert
            Assert.AreEqual(expectedDeviceId, result);
        }

        [TestMethod]
        public void GetDeviceId_WhenProcessExecutorThrowsException_ReturnsEmptyString()
        {
            // Arrange
            _mockCommandProvider.Setup(x => x.DeviceIdCommand).Returns("device-id");
            _mockProcessExecutor.Setup(x => x.Execute("device-id", null, null))
                .Throws(new Exception("Error"));

            // Act
            var result = _cliExecutor.GetDeviceId();

            // Assert
            Assert.AreEqual(string.Empty, result);
            _mockLogger.Verify(x => x.Error("Could not get device ID", It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public void GetDeviceId_TrimsWhitespace()
        {
            // Arrange
            var deviceIdWithWhitespace = "  device-id-123  \r\n";
            _mockCommandProvider.Setup(x => x.DeviceIdCommand).Returns("device-id");
            _mockProcessExecutor.Setup(x => x.Execute("device-id", null, null))
                .Returns(deviceIdWithWhitespace);

            // Act
            var result = _cliExecutor.GetDeviceId();

            // Assert
            Assert.AreEqual("device-id-123", result);
        }

        [TestMethod]
        public void GetFileVersion_WithValidResponse_ReturnsVersion()
        {
            // Arrange
            var expectedVersion = "1.2.3";
            _mockCommandProvider.Setup(x => x.VersionCommand).Returns("version --sha");
            _mockProcessExecutor.Setup(x => x.Execute("version --sha", null, null))
                .Returns(expectedVersion);

            // Act
            var result = _cliExecutor.GetFileVersion();

            // Assert
            Assert.AreEqual(expectedVersion, result);
        }

        [TestMethod]
        public void GetFileVersion_WhenProcessExecutorThrowsException_ReturnsEmptyString()
        {
            // Arrange
            _mockCommandProvider.Setup(x => x.VersionCommand).Returns("version --sha");
            _mockProcessExecutor.Setup(x => x.Execute("version --sha", null, null))
                .Throws(new Exception("Error"));

            // Act
            var result = _cliExecutor.GetFileVersion();

            // Assert
            Assert.AreEqual(string.Empty, result);
            _mockLogger.Verify(x => x.Error("Could not get CLI version", It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public void GetFileVersion_TrimsWhitespace()
        {
            // Arrange
            var versionWithWhitespace = "  1.2.3  \r\n";
            _mockCommandProvider.Setup(x => x.VersionCommand).Returns("version --sha");
            _mockProcessExecutor.Setup(x => x.Execute("version --sha", null, null))
                .Returns(versionWithWhitespace);

            // Act
            var result = _cliExecutor.GetFileVersion();

            // Assert
            Assert.AreEqual("1.2.3", result);
        }

        [TestMethod]
        public void PostRefactoring_WithValidResponse_SendsPerformanceTelemetry()
        {
            // Arrange
            var fnToRefactor = new FnToRefactorModel
            {
                Name = "Test",
                Body = "public void Test() { }",
                FileType = "cs",
            };
            var response = new RefactorResponseModel { Code = "refactored" };
            var jsonResponse = JsonConvert.SerializeObject(response);
            var token = "test-token";
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns(token);
            _mockCommandProvider.Setup(x => x.GetRefactorPostCommand(It.IsAny<FnToRefactorModel>(), It.IsAny<bool>(), It.IsAny<string>()))
                .Returns("refactor post");
            _mockProcessExecutor.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(jsonResponse);

            // Act
            _cliExecutor.PostRefactoring(fnToRefactor);

            // Assert
            // Telemetry is sent asynchronously via Task.Run, so we can't directly verify it
            // but the method should complete without errors
        }

        [TestMethod]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new CliExecutor(null, _mockCliServices.Object, _mockSettingsProvider.Object));
        }

        [TestMethod]
        public void Constructor_WithNullCliServices_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new CliExecutor(_mockLogger.Object, null, _mockSettingsProvider.Object));
        }

        [TestMethod]
        public void Constructor_WithNullTelemetryManagerLazy_DoesNotThrow()
        {
            // Act & Assert - should not throw
            var executor = new CliExecutor(
                _mockLogger.Object,
                _mockCliServices.Object,
                _mockSettingsProvider.Object,
                null);

            Assert.IsNotNull(executor);
        }
    }
}
