// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using System.Security.Cryptography;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Moq;
using Nerdbank.Streams;
using StreamJsonRpc;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class IdeServerClientRestartTests
    {
        private string _tempFilePath;
        private Mock<ICliSettingsProvider> _settings;
        private Mock<ILogger> _logger;
        private Mock<ISettingsProvider> _userSettings;

        [TestInitialize]
        public void Setup()
        {
            _tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");
            File.WriteAllText(_tempFilePath, "dummy-cli");
            _settings = new Mock<ICliSettingsProvider>();
            _logger = new Mock<ILogger>();
            _userSettings = new Mock<ISettingsProvider>();
            _settings.Setup(x => x.CliFileFullPath).Returns(_tempFilePath);
            _settings.Setup(x => x.RequiredDevToolVersion).Returns("fixture-sha");
            _settings.Setup(x => x.RequiredCliBinarySha256).Returns(Sha256(_tempFilePath));
            _userSettings.Setup(x => x.ServerWorkerThreads).Returns(0);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }

        [TestMethod]
        public async Task RestartAsync_RestartsServerProcess()
        {
            var servers = new List<(JsonRpc Rpc, Stream Stream)>();
            var factory = new RestartableProcessFactory(servers);
            using var client = new IdeServerClient(
                _settings.Object,
                _logger.Object,
                factory,
                _userSettings.Object,
                TimeSpan.FromSeconds(5));
            await client.StartAsync();
            await client.RestartAsync();

            Assert.AreEqual(2, factory.StartCount);
            _logger.Verify(x => x.Info("[cs-ide] restarting"), Times.Once);
            foreach (var (rpc, stream) in servers)
            {
                rpc.Dispose();
                stream.Dispose();
            }
        }

        private static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private sealed class RestartableProcessFactory : IIdeServerProcessFactory
        {
            private readonly List<(JsonRpc Rpc, Stream Stream)> _servers;

            public RestartableProcessFactory(List<(JsonRpc Rpc, Stream Stream)> servers)
            {
                _servers = servers;
            }

            public int StartCount { get; private set; }

            public IIdeServerProcess Start(string fileName, string arguments)
            {
                StartCount++;
                var (clientStream, serverStream) = FullDuplexStream.CreatePair();
                var process = new FakeIdeServerProcess(clientStream, clientStream);
                var serverRpc = new JsonRpc(new HeaderDelimitedMessageHandler(serverStream, serverStream));
                var server = new FixtureServer(serverRpc);
                serverRpc.AddLocalRpcTarget(server);
                serverRpc.StartListening();
                _servers.Add((serverRpc, serverStream));
                _ = Task.Run(async () =>
                {
                    await Task.Yield();
                    await serverRpc.NotifyWithParameterObjectAsync("cs-ide/start", new { sha = "fixture-sha", version = "fixture-version" });
                });
                return process;
            }
        }
    }
}
