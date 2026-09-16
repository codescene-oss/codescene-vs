// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliExecutorRefactorDeviceTests
    {
        private const string TestCachePath = "/test/cache/path";
        private const string TestFileName = "test.cs";
        private const string TestFileContent = "public class Test { }";

        private Mock<ILogger> _mockLogger;
        private Mock<IIdeServerClient> _mockClient;
        private Mock<ICacheStorageService> _mockCacheStorage;
        private Mock<ISettingsProvider> _mockSettingsProvider;
        private CliExecutor _cliExecutor;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockClient = new Mock<IIdeServerClient>();
            _mockCacheStorage = new Mock<ICacheStorageService>();
            _mockSettingsProvider = new Mock<ISettingsProvider>();
            _mockCacheStorage.Setup(x => x.GetSolutionReviewCacheLocation()).Returns(TestCachePath);
            _cliExecutor = new CliExecutor(_mockLogger.Object, _mockClient.Object, _mockCacheStorage.Object, _mockSettingsProvider.Object, null);
        }

        [TestMethod]
        public async Task PostRefactoring_WithValidResponse_ReturnsRefactorResponseModel()
        {
            var fnToRefactor = new FnToRefactorModel { Name = "TestMethod", Body = "public void Test() { }", FileType = "cs" };
            var expectedResponse = new RefactorResponseModel { Code = "public void Test() { /* refactored */ }", TraceId = "trace-123" };
            var token = "test-token";
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns(token);
            _mockClient.Setup(x => x.RefactorAsync(It.IsAny<RefactorPostRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            var result = await _cliExecutor.PostRefactoringAsync(fnToRefactor, skipCache: false);

            Assert.IsNotNull(result);
            Assert.AreEqual(expectedResponse.Code, result.Code);
            Assert.AreEqual(expectedResponse.TraceId, result.TraceId);
            _mockClient.Verify(
                x => x.RefactorAsync(
                    It.Is<RefactorPostRequestModel>(r => r.Token == token && r.FnToRefactor == fnToRefactor && r.SkipCache == null),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task PostRefactoring_WithMissingAuthToken_ThrowsMissingAuthTokenException()
        {
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns(string.Empty);

            var exception = await Assert.ThrowsAsync<MissingAuthTokenException>(() => _cliExecutor.PostRefactoringAsync(fnToRefactor));

            Assert.Contains("Authentication token is missing", exception.Message);
            _mockLogger.Verify(
                x => x.Warn(It.Is<string>(s => s.Contains("Refactoring failed") && s.Contains("Authentication token is missing")), It.IsAny<bool>()),
                Times.Once);
            _mockLogger.Verify(x => x.Error(It.IsAny<string>(), It.IsAny<Exception>()), Times.Never);
        }

        [TestMethod]
        public async Task PostRefactoring_WithProvidedToken_UsesProvidedToken()
        {
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            var providedToken = "provided-token";
            var response = new RefactorResponseModel { Code = "refactored" };
            _mockClient.Setup(x => x.RefactorAsync(It.IsAny<RefactorPostRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var result = await _cliExecutor.PostRefactoringAsync(fnToRefactor, skipCache: false, token: providedToken);

            Assert.IsNotNull(result);
            _mockClient.Verify(
                x => x.RefactorAsync(
                    It.Is<RefactorPostRequestModel>(r => r.Token == providedToken && r.FnToRefactor == fnToRefactor),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task PostRefactoring_WhenClientThrowsException_ReturnsNull()
        {
            var fnToRefactor = new FnToRefactorModel { Name = "Test" };
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns("test-token");
            _mockClient.Setup(x => x.RefactorAsync(It.IsAny<RefactorPostRequestModel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Error"));

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
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns(token);
            _mockClient.Setup(x => x.RefactorAsync(It.IsAny<RefactorPostRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(response);

            var result = await _cliExecutor.PostRefactoringAsync(fnToRefactor, skipCache: true);

            Assert.IsNotNull(result);
            _mockClient.Verify(
                x => x.RefactorAsync(
                    It.Is<RefactorPostRequestModel>(r => r.SkipCache == true && r.Token == token),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task FnsToRefactorFromCodeSmells_WithValidResponse_ReturnsListOfFnToRefactor()
        {
            var codeSmells = new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "Complex Method" } };
            var preflight = new PreFlightResponseModel { Version = 1.0m };
            var expectedFunctions = new List<FnToRefactorModel> { new FnToRefactorModel { Name = "Function1", Body = "code" } };
            _mockClient.Setup(x => x.FnsToRefactorAsync(It.IsAny<FnsToRefactorRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedFunctions);

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
        public async Task FnsToRefactorFromCodeSmells_RemovesOldCacheEntries()
        {
            var codeSmells = new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "Test" } };
            _mockClient.Setup(x => x.FnsToRefactorAsync(It.IsAny<FnsToRefactorRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FnToRefactorModel>());

            await _cliExecutor.FnsToRefactorFromCodeSmellsAsync(TestFileName, TestFileContent, codeSmells, null);

            _mockCacheStorage.Verify(x => x.RemoveOldReviewCacheEntries(), Times.Once);
        }

        [TestMethod]
        public async Task FnsToRefactorFromDelta_WithValidResponse_ReturnsListOfFnToRefactor()
        {
            var deltaResult = new DeltaResponseModel { NewScore = 8.0m, OldScore = 7.0m };
            var preflight = new PreFlightResponseModel { Version = 1.0m };
            var expectedFunctions = new List<FnToRefactorModel> { new FnToRefactorModel { Name = "Function1", Body = "code" } };
            _mockClient.Setup(x => x.FnsToRefactorAsync(It.IsAny<FnsToRefactorRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedFunctions);

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
        public async Task FnsToRefactorFromDelta_RemovesOldCacheEntries()
        {
            var deltaResult = new DeltaResponseModel { NewScore = 8.0m, OldScore = 7.0m };
            _mockClient.Setup(x => x.FnsToRefactorAsync(It.IsAny<FnsToRefactorRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FnToRefactorModel>());

            await _cliExecutor.FnsToRefactorFromDeltaAsync(TestFileName, TestFileContent, deltaResult, null);

            _mockCacheStorage.Verify(x => x.RemoveOldReviewCacheEntries(), Times.Once);
        }

        [TestMethod]
        public async Task FnsToRefactorFromDelta_ConcurrentIdenticalRequests_CallsClientOnce()
        {
            var deltaResult = new DeltaResponseModel { NewScore = 8.0m, OldScore = 7.0m };
            var functions = new List<FnToRefactorModel> { new FnToRefactorModel { Name = "Function1", Body = "code" } };
            var completion = new TaskCompletionSource<bool>();
            var callCount = 0;

            _mockClient.Setup(x => x.FnsToRefactorAsync(It.IsAny<FnsToRefactorRequestModel>(), It.IsAny<CancellationToken>()))
                .Returns<FnsToRefactorRequestModel, CancellationToken>(async (_, __) =>
                {
                    Interlocked.Increment(ref callCount);
                    await completion.Task;
                    return functions;
                });

            var firstTask = _cliExecutor.FnsToRefactorFromDeltaAsync(TestFileName, TestFileContent, deltaResult, null);
            var secondTask = _cliExecutor.FnsToRefactorFromDeltaAsync(TestFileName, TestFileContent, deltaResult, null);
            await Task.Delay(100);

            Assert.AreEqual(1, callCount, "Identical refactorability requests should share one in-flight command.");

            completion.TrySetResult(true);
            var firstResult = await firstTask;
            var secondResult = await secondTask;

            Assert.HasCount(1, firstResult);
            Assert.HasCount(1, secondResult);
            Assert.AreEqual(1, callCount);
        }

        [TestMethod]
        public async Task FnsToRefactorFromDelta_CanceledCallerDoesNotCancelSharedRequest()
        {
            var deltaResult = new DeltaResponseModel { NewScore = 8.0m, OldScore = 7.0m };
            var functions = new List<FnToRefactorModel> { new FnToRefactorModel { Name = "Function1", Body = "code" } };
            var entered = new TaskCompletionSource<bool>();
            var completion = new TaskCompletionSource<IList<FnToRefactorModel>>();
            var capturedToken = CancellationToken.None;

            _mockClient.Setup(x => x.FnsToRefactorAsync(It.IsAny<FnsToRefactorRequestModel>(), It.IsAny<CancellationToken>()))
                .Returns<FnsToRefactorRequestModel, CancellationToken>((_, token) =>
                {
                    capturedToken = token;
                    entered.TrySetResult(true);
                    return completion.Task;
                });

            var cts = new CancellationTokenSource();
            var canceledCallerTask = _cliExecutor.FnsToRefactorFromDeltaAsync(TestFileName, TestFileContent, deltaResult, null, cts.Token);
            await entered.Task;

            var secondCallerTask = _cliExecutor.FnsToRefactorFromDeltaAsync(TestFileName, TestFileContent, deltaResult, null);
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => canceledCallerTask);

            completion.TrySetResult(functions);
            var secondResult = await secondCallerTask;

            Assert.HasCount(1, secondResult);
            Assert.AreEqual(CancellationToken.None, capturedToken);
            _mockClient.Verify(x => x.FnsToRefactorAsync(It.IsAny<FnsToRefactorRequestModel>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task GetDeviceId_WithValidResponse_ReturnsDeviceId()
        {
            _mockClient.Setup(x => x.DeviceIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync("device-id-123");

            var result = await _cliExecutor.GetDeviceIdAsync();

            Assert.AreEqual("device-id-123", result);
        }

        [TestMethod]
        public async Task GetDeviceId_WhenClientThrowsException_ReturnsEmptyString()
        {
            _mockClient.Setup(x => x.DeviceIdAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Error"));

            var result = await _cliExecutor.GetDeviceIdAsync();

            Assert.AreEqual(string.Empty, result);
            _mockLogger.Verify(x => x.Error("Could not get device ID", It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task GetDeviceId_TrimsWhitespace()
        {
            _mockClient.Setup(x => x.DeviceIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync("  device-id-123  \r\n");

            var result = await _cliExecutor.GetDeviceIdAsync();

            Assert.AreEqual("device-id-123", result);
        }

        [TestMethod]
        public async Task GetFileVersion_WithValidResponse_ReturnsVersion()
        {
            _mockClient.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServerMetadata { Sha = "1.2.3" });

            var result = await _cliExecutor.GetFileVersionAsync();

            Assert.AreEqual("1.2.3", result);
        }

        [TestMethod]
        public async Task GetFileVersion_WhenClientThrowsException_ReturnsEmptyString()
        {
            _mockClient.Setup(x => x.StartAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Error"));

            var result = await _cliExecutor.GetFileVersionAsync();

            Assert.AreEqual(string.Empty, result);
            _mockLogger.Verify(x => x.Error("Could not get CLI version", It.IsAny<Exception>()), Times.Once);
        }

        [TestMethod]
        public async Task GetFileVersion_TrimsWhitespace()
        {
            _mockClient.Setup(x => x.StartAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ServerMetadata { Sha = "  1.2.3  \r\n" });

            var result = await _cliExecutor.GetFileVersionAsync();

            Assert.AreEqual("1.2.3", result);
        }
    }
}
