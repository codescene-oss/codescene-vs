// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Moq;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliExecutorRefactorDeviceTests
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
        private CliExecutor _cliExecutor;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockCommandProvider = new Mock<ICliCommandProvider>();
            _mockProcessExecutor = new Mock<IProcessExecutor>();
            _mockCacheStorage = new Mock<ICacheStorageService>();
            _mockSettingsProvider = new Mock<ISettingsProvider>();

            _mockCliServices = new Mock<ICliServices>();
            _mockCliServices.Setup(x => x.CommandProvider).Returns(_mockCommandProvider.Object);
            _mockCliServices.Setup(x => x.ProcessExecutor).Returns(_mockProcessExecutor.Object);
            _mockCliServices.Setup(x => x.CacheStorage).Returns(_mockCacheStorage.Object);
            _mockCacheStorage.Setup(x => x.GetSolutionReviewCacheLocation()).Returns(TestCachePath);

            _cliExecutor = new CliExecutor(_mockLogger.Object, _mockCliServices.Object, _mockSettingsProvider.Object, null);
        }

        [TestMethod]
        public async Task PostRefactoring_WithValidResponse_ReturnsRefactorResponseModel()
        {
            var fnToRefactor = new FnToRefactorModel { Name = "TestMethod", Body = "public void Test() { }", FileType = "cs" };
            var expectedResponse = new RefactorResponseModel { Code = "public void Test() { /* refactored */ }", TraceId = "trace-123" };
            var jsonResponse = JsonConvert.SerializeObject(expectedResponse);
            var token = "test-token";
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns(token);
            _mockCommandProvider.Setup(x => x.GetRefactorPostCommand(fnToRefactor, false, token)).Returns("refactor post command");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("refactor post command", It.IsAny<string>(), null, default)).ReturnsAsync(jsonResponse);

            var result = await _cliExecutor.PostRefactoringAsync(fnToRefactor, skipCache: false);

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResponse.Code, result.Code);
            Assert.AreEqual(expectedResponse.TraceId, result.TraceId);
        }

        [TestMethod]
        public async Task PostRefactoring_WithMissingAuthToken_ThrowsMissingAuthTokenException()
        {
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns(string.Empty);

            var exception = await Assert.ThrowsAsync<MissingAuthTokenException>(() => _cliExecutor.PostRefactoringAsync(fnToRefactor));

            Assert.Contains("Authentication token is missing", exception.Message);
        }

        [TestMethod]
        public async Task PostRefactoring_WithProvidedToken_UsesProvidedToken()
        {
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            var providedToken = "provided-token";
            var response = new RefactorResponseModel { Code = "refactored" };
            var jsonResponse = JsonConvert.SerializeObject(response);
            _mockCommandProvider.Setup(x => x.GetRefactorPostCommand(fnToRefactor, false, providedToken)).Returns("refactor post");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(jsonResponse);

            var result = await _cliExecutor.PostRefactoringAsync(fnToRefactor, skipCache: false, token: providedToken);

            Assert.IsNotNull(result);
            _mockCommandProvider.Verify(x => x.GetRefactorPostCommand(fnToRefactor, false, providedToken), Times.Once);
        }

        [TestMethod]
        public async Task PostRefactoring_WithEmptyArguments_ReturnsNull()
        {
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns("test-token");
            _mockCommandProvider.Setup(x => x.GetRefactorPostCommand(It.IsAny<FnToRefactorModel>(), It.IsAny<bool>(), It.IsAny<string>())).Returns(string.Empty);

            var result = await _cliExecutor.PostRefactoringAsync(fnToRefactor);

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Warn("Skipping refactoring. Arguments were not defined."), Times.Once);
        }

        [TestMethod]
        public async Task PostRefactoring_WhenProcessExecutorThrowsException_ReturnsNull()
        {
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns("test-token");
            _mockCommandProvider.Setup(x => x.GetRefactorPostCommand(It.IsAny<FnToRefactorModel>(), It.IsAny<bool>(), It.IsAny<string>())).Returns("refactor post");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<System.Threading.CancellationToken>())).ThrowsAsync(new Exception("Error"));

            var result = await _cliExecutor.PostRefactoringAsync(fnToRefactor);

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Error(It.Is<string>(s => s.Contains("Refactoring failed")), It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task PostRefactoring_WithSkipCache_PassesSkipCacheFlag()
        {
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            var token = "test-token";
            var response = new RefactorResponseModel { Code = "refactored" };
            var jsonResponse = JsonConvert.SerializeObject(response);
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns(token);
            _mockCommandProvider.Setup(x => x.GetRefactorPostCommand(fnToRefactor, true, token)).Returns("refactor post --skip-cache");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(jsonResponse);

            var result = await _cliExecutor.PostRefactoringAsync(fnToRefactor, skipCache: true);

            Assert.IsNotNull(result);
            _mockCommandProvider.Verify(x => x.GetRefactorPostCommand(fnToRefactor, true, token), Times.Once);
        }

        [TestMethod]
        public async Task FnsToRefactorFromCodeSmells_WithValidResponse_ReturnsListOfFnToRefactor()
        {
            var codeSmells = new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "Complex Method" } };
            var preflight = new PreFlightResponseModel { Version = 1.0m };
            var expectedFunctions = new List<FnToRefactorModel> { new FnToRefactorModel { Name = "Function1", Body = "code" } };
            var jsonResponse = JsonConvert.SerializeObject(expectedFunctions);
            _mockCommandProvider.Setup(x => x.GetRefactorWithCodeSmellsPayload(TestFileName, TestFileContent, TestCachePath, codeSmells, preflight)).Returns("payload");
            _mockCommandProvider.Setup(x => x.RefactorCommand).Returns("refactor");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("refactor", "payload", null, default)).ReturnsAsync(jsonResponse);

            var result = await _cliExecutor.FnsToRefactorFromCodeSmellsAsync(TestFileName, TestFileContent, codeSmells, preflight);

            Assert.IsNotNull(result);
            Assert.HasCount(1, result);
            Assert.AreEqual("Function1", result[0].Name);
        }

        [TestMethod]
        public async Task FnsToRefactorFromCodeSmells_WithNullCodeSmells_ReturnsNull()
        {
            var result = await _cliExecutor.FnsToRefactorFromCodeSmellsAsync(TestFileName, TestFileContent, null, null);

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Debug("Skipping refactoring functions from code smells. Code smells list was null or empty."), Times.Once);
        }

        [TestMethod]
        public async Task FnsToRefactorFromCodeSmells_WithEmptyCodeSmells_ReturnsNull()
        {
            var result = await _cliExecutor.FnsToRefactorFromCodeSmellsAsync(TestFileName, TestFileContent, new List<CliCodeSmellModel>(), null);

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Debug("Skipping refactoring functions from code smells. Code smells list was null or empty."), Times.Once);
        }

        [TestMethod]
        public async Task FnsToRefactorFromCodeSmells_WithEmptyPayload_ReturnsNull()
        {
            var codeSmells = new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "Test" } };
            _mockCommandProvider.Setup(x => x.GetRefactorWithCodeSmellsPayload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<CliCodeSmellModel>>(), It.IsAny<PreFlightResponseModel>())).Returns(string.Empty);

            var result = await _cliExecutor.FnsToRefactorFromCodeSmellsAsync(TestFileName, TestFileContent, codeSmells, null);

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Warn("Skipping refactoring functions check. Payload content was not defined."), Times.Once);
        }

        [TestMethod]
        public async Task FnsToRefactorFromCodeSmells_RemovesOldCacheEntries()
        {
            var codeSmells = new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "Test" } };
            var jsonResponse = JsonConvert.SerializeObject(new List<FnToRefactorModel>());
            _mockCommandProvider.Setup(x => x.GetRefactorWithCodeSmellsPayload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<CliCodeSmellModel>>(), It.IsAny<PreFlightResponseModel>())).Returns("payload");
            _mockCommandProvider.Setup(x => x.RefactorCommand).Returns("refactor");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(jsonResponse);

            await _cliExecutor.FnsToRefactorFromCodeSmellsAsync(TestFileName, TestFileContent, codeSmells, null);

            _mockCacheStorage.Verify(x => x.RemoveOldReviewCacheEntries(), Times.Once);
        }

        [TestMethod]
        public async Task FnsToRefactorFromDelta_WithValidResponse_ReturnsListOfFnToRefactor()
        {
            var deltaResult = new DeltaResponseModel { NewScore = 8.0m, OldScore = 7.0m };
            var preflight = new PreFlightResponseModel { Version = 1.0m };
            var expectedFunctions = new List<FnToRefactorModel> { new FnToRefactorModel { Name = "Function1", Body = "code" } };
            var jsonResponse = JsonConvert.SerializeObject(expectedFunctions);
            _mockCommandProvider.Setup(x => x.GetRefactorWithDeltaResultPayload(TestFileName, TestFileContent, TestCachePath, deltaResult, preflight)).Returns("payload");
            _mockCommandProvider.Setup(x => x.RefactorCommand).Returns("refactor");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("refactor", "payload", null, default)).ReturnsAsync(jsonResponse);

            var result = await _cliExecutor.FnsToRefactorFromDeltaAsync(TestFileName, TestFileContent, deltaResult, preflight);

            Assert.IsNotNull(result);
            Assert.HasCount(1, result);
            Assert.AreEqual("Function1", result[0].Name);
        }

        [TestMethod]
        public async Task FnsToRefactorFromDelta_WithNullDeltaResult_ReturnsNull()
        {
            var result = await _cliExecutor.FnsToRefactorFromDeltaAsync(TestFileName, TestFileContent, null, null);

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Debug("Skipping refactoring functions from delta. Delta result was null."), Times.Once);
        }

        [TestMethod]
        public async Task FnsToRefactorFromDelta_WithEmptyPayload_ReturnsNull()
        {
            var deltaResult = new DeltaResponseModel { NewScore = 8.0m, OldScore = 7.0m };
            _mockCommandProvider.Setup(x => x.GetRefactorWithDeltaResultPayload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DeltaResponseModel>(), It.IsAny<PreFlightResponseModel>())).Returns(string.Empty);

            var result = await _cliExecutor.FnsToRefactorFromDeltaAsync(TestFileName, TestFileContent, deltaResult, null);

            Assert.IsNull(result);
            _mockLogger.Verify(x => x.Warn("Skipping refactoring functions check. Payload content was not defined."), Times.Once);
        }

        [TestMethod]
        public async Task FnsToRefactorFromDelta_RemovesOldCacheEntries()
        {
            var deltaResult = new DeltaResponseModel { NewScore = 8.0m, OldScore = 7.0m };
            var jsonResponse = JsonConvert.SerializeObject(new List<FnToRefactorModel>());
            _mockCommandProvider.Setup(x => x.GetRefactorWithDeltaResultPayload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DeltaResponseModel>(), It.IsAny<PreFlightResponseModel>())).Returns("payload");
            _mockCommandProvider.Setup(x => x.RefactorCommand).Returns("refactor");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<System.Threading.CancellationToken>())).ReturnsAsync(jsonResponse);

            await _cliExecutor.FnsToRefactorFromDeltaAsync(TestFileName, TestFileContent, deltaResult, null);

            _mockCacheStorage.Verify(x => x.RemoveOldReviewCacheEntries(), Times.Once);
        }

        [TestMethod]
        public async Task GetDeviceId_WithValidResponse_ReturnsDeviceId()
        {
            var expectedDeviceId = "device-id-123";
            _mockCommandProvider.Setup(x => x.DeviceIdCommand).Returns("device-id");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("device-id", null, null, default)).ReturnsAsync(expectedDeviceId);

            var result = await _cliExecutor.GetDeviceIdAsync();

            Assert.AreEqual(expectedDeviceId, result);
        }

        [TestMethod]
        public async Task GetDeviceId_WhenProcessExecutorThrowsException_ReturnsEmptyString()
        {
            _mockCommandProvider.Setup(x => x.DeviceIdCommand).Returns("device-id");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("device-id", null, null, default)).ThrowsAsync(new Exception("Error"));

            var result = await _cliExecutor.GetDeviceIdAsync();

            Assert.AreEqual(string.Empty, result);
            _mockLogger.Verify(x => x.Error("Could not get device ID", It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task GetDeviceId_TrimsWhitespace()
        {
            var deviceIdWithWhitespace = "  device-id-123  \r\n";
            _mockCommandProvider.Setup(x => x.DeviceIdCommand).Returns("device-id");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("device-id", null, null, default)).ReturnsAsync(deviceIdWithWhitespace);

            var result = await _cliExecutor.GetDeviceIdAsync();

            Assert.AreEqual("device-id-123", result);
        }

        [TestMethod]
        public async Task GetFileVersion_WithValidResponse_ReturnsVersion()
        {
            var expectedVersion = "1.2.3";
            _mockCommandProvider.Setup(x => x.VersionCommand).Returns("version --sha");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("version --sha", null, null, default)).ReturnsAsync(expectedVersion);

            var result = await _cliExecutor.GetFileVersionAsync();

            Assert.AreEqual(expectedVersion, result);
        }

        [TestMethod]
        public async Task GetFileVersion_WhenProcessExecutorThrowsException_ReturnsEmptyString()
        {
            _mockCommandProvider.Setup(x => x.VersionCommand).Returns("version --sha");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("version --sha", null, null, default)).ThrowsAsync(new Exception("Error"));

            var result = await _cliExecutor.GetFileVersionAsync();

            Assert.AreEqual(string.Empty, result);
            _mockLogger.Verify(x => x.Error("Could not get CLI version", It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task GetFileVersion_TrimsWhitespace()
        {
            var versionWithWhitespace = "  1.2.3  \r\n";
            _mockCommandProvider.Setup(x => x.VersionCommand).Returns("version --sha");
            _mockProcessExecutor.Setup(x => x.ExecuteAsync("version --sha", null, null, default)).ReturnsAsync(versionWithWhitespace);

            var result = await _cliExecutor.GetFileVersionAsync();

            Assert.AreEqual("1.2.3", result);
        }
    }
}
