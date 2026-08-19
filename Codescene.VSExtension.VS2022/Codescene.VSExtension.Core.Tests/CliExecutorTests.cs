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
    public class CliExecutorTests
    {
        private Mock<ILogger> _logger;
        private Mock<IIdeServerHost> _host;
        private Mock<IIdeServerClient> _client;
        private Mock<ICacheStorageService> _cache;
        private Mock<ISettingsProvider> _settings;
        private CliExecutor _executor;

        [TestInitialize]
        public void Setup()
        {
            _logger = new Mock<ILogger>();
            _client = new Mock<IIdeServerClient>();
            _host = new Mock<IIdeServerHost>();
            _host.Setup(h => h.Client).Returns(_client.Object);
            _host.Setup(h => h.Metadata).Returns(new ServerStartMetadata { Sha = "abc123", Version = "1.0" });
            _cache = new Mock<ICacheStorageService>();
            _cache.Setup(c => c.GetSolutionReviewCacheLocation()).Returns("/cache");
            _settings = new Mock<ISettingsProvider>();
            _executor = new CliExecutor(_logger.Object, _host.Object, _cache.Object, _settings.Object);
        }

        [TestMethod]
        public async Task ReviewContentAsync_CallsReviewRpc()
        {
            _client.Setup(c => c.ReviewAsync(It.IsAny<ReviewRequestModel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CliReviewModel { Score = 8, RawScore = "raw" });

            var result = await _executor.ReviewContentAsync("a.cs", "code");

            Assert.AreEqual(8, result.Score);
            _client.Verify(
                c => c.ReviewAsync(It.Is<ReviewRequestModel>(r => r.FilePath == "a.cs" && r.FileContent == "code"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [TestMethod]
        public async Task ReviewDeltaAsync_CallsDeltaRpc()
        {
            _client.Setup(c => c.DeltaAsync(It.IsAny<DeltaRequestParams>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DeltaResponseModel { ScoreChange = -1 });

            var result = await _executor.ReviewDeltaAsync(new ReviewDeltaRequest { OldScore = "old", NewScore = "new", FilePath = "a.cs" });

            Assert.AreEqual(-1, result.ScoreChange);
        }

        [TestMethod]
        public async Task PreflightAsync_CallsPreflightRpc()
        {
            _client.Setup(c => c.PreflightAsync(true, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PreFlightResponseModel());

            var result = await _executor.PreflightAsync();

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task GetDeviceIdAsync_ReturnsClientValue()
        {
            _client.Setup(c => c.DeviceIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync("dev-1");

            var result = await _executor.GetDeviceIdAsync();

            Assert.AreEqual("dev-1", result);
        }

        [TestMethod]
        public async Task GetFileVersionAsync_ReturnsHostSha()
        {
            var result = await _executor.GetFileVersionAsync();
            Assert.AreEqual("abc123", result);
        }

        [TestMethod]
        public async Task PostRefactoringAsync_MissingToken_Throws()
        {
            _settings.Setup(s => s.AuthToken).Returns((string)null);

            await Assert.ThrowsAsync<MissingAuthTokenException>(() =>
                _executor.PostRefactoringAsync(new FnToRefactorModel { Name = "f" }));
        }

        [TestMethod]
        public void Constructor_NullHost_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new CliExecutor(_logger.Object, null, _cache.Object, _settings.Object));
        }
    }
}
