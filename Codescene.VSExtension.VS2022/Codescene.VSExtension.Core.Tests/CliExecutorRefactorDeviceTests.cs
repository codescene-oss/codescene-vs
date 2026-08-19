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
        private Mock<ILogger> _mockLogger;
        private Mock<IIdeServerHost> _mockHost;
        private Mock<IIdeServerClient> _mockClient;
        private Mock<ICacheStorageService> _mockCacheStorage;
        private Mock<ISettingsProvider> _mockSettingsProvider;
        private CliExecutor _cliExecutor;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockClient = new Mock<IIdeServerClient>();
            _mockHost = new Mock<IIdeServerHost>();
            _mockHost.Setup(x => x.Client).Returns(_mockClient.Object);
            _mockHost.Setup(x => x.Metadata).Returns(new ServerStartMetadata { Sha = "sha" });
            _mockCacheStorage = new Mock<ICacheStorageService>();
            _mockSettingsProvider = new Mock<ISettingsProvider>();
            _mockCacheStorage.Setup(x => x.GetSolutionReviewCacheLocation()).Returns("/cache");
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns("token");
            _cliExecutor = new CliExecutor(_mockLogger.Object, _mockHost.Object, _mockCacheStorage.Object, _mockSettingsProvider.Object);
        }

        [TestMethod]
        public async Task PostRefactoring_WithValidResponse_ReturnsRefactorResponseModel()
        {
            var fn = new FnToRefactorModel { Name = "TestMethod", Body = "public void Test() { }", FileType = "cs" };
            var expected = new RefactorResponseModel { Code = "refactored", TraceId = "trace-123" };
            _mockClient.Setup(c => c.RefactorAsync(It.IsAny<RefactorPostRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            var result = await _cliExecutor.PostRefactoringAsync(fn);

            Assert.AreEqual("refactored", result.Code);
        }

        [TestMethod]
        public async Task PostRefactoring_WithMissingAuthToken_ThrowsMissingAuthTokenException()
        {
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns((string)null);

            await Assert.ThrowsAsync<MissingAuthTokenException>(() =>
                _cliExecutor.PostRefactoringAsync(new FnToRefactorModel { Name = "f" }));
        }

        [TestMethod]
        public async Task PostRefactoring_WithProvidedToken_UsesProvidedToken()
        {
            RefactorPostRequestModel? captured = null;
            _mockClient.Setup(c => c.RefactorAsync(It.IsAny<RefactorPostRequestModel>(), It.IsAny<CancellationToken>()))
                .Callback<RefactorPostRequestModel, CancellationToken>((req, _) => captured = req)
                .ReturnsAsync(new RefactorResponseModel());

            await _cliExecutor.PostRefactoringAsync(new FnToRefactorModel { Name = "f" }, token: "provided");

            Assert.AreEqual("provided", captured!.Token);
        }

        [TestMethod]
        public async Task FnsToRefactorFromCodeSmells_WithNullCodeSmells_ReturnsNull()
        {
            var result = await _cliExecutor.FnsToRefactorFromCodeSmellsAsync("a.cs", "code", null, new PreFlightResponseModel());
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task FnsToRefactorFromDelta_WithNullDeltaResult_ReturnsNull()
        {
            var result = await _cliExecutor.FnsToRefactorFromDeltaAsync("a.cs", "code", null, new PreFlightResponseModel());
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task FnsToRefactorFromDelta_RemovesOldCacheEntries()
        {
            _mockClient.Setup(c => c.FnsToRefactorAsync(It.IsAny<FnsToRefactorRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<FnToRefactorModel>());

            await _cliExecutor.FnsToRefactorFromDeltaAsync("a.cs", "code", new DeltaResponseModel(), new PreFlightResponseModel());

            _mockCacheStorage.Verify(c => c.RemoveOldReviewCacheEntries(30), Times.Once);
        }

        [TestMethod]
        public async Task GetDeviceId_WhenClientThrows_ReturnsEmptyString()
        {
            _mockClient.Setup(c => c.DeviceIdAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("fail"));

            var result = await _cliExecutor.GetDeviceIdAsync();

            Assert.AreEqual(string.Empty, result);
        }
    }
}
