// Copyright (c) CodeScene. All rights reserved.

using System.Security.Cryptography;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Moq;
using Nerdbank.Streams;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Codescene.VSExtension.Core.Tests
{
    internal sealed class ContractClientHarness : IDisposable
    {
        private readonly string _executable;
        private readonly Stream _serverStream;

        private ContractClientHarness(IdeServerClient client, JsonRpc serverRpc, Stream serverStream, string executable)
        {
            Client = client;
            ServerRpc = serverRpc;
            _serverStream = serverStream;
            _executable = executable;
        }

        public IdeServerClient Client { get; }

        public JsonRpc ServerRpc { get; }

        public static ContractClientHarness Create(JObject startWire, object? serverTarget = null)
        {
            var executable = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");
            System.IO.File.WriteAllText(executable, "dummy-cli");
            var settings = new Mock<ICliSettingsProvider>();
            settings.Setup(x => x.CliFileFullPath).Returns(executable);
            settings.Setup(x => x.RequiredDevToolVersion).Returns((string?)startWire["sha"] ?? throw new InvalidOperationException("The start fixture has no sha"));
            settings.Setup(x => x.RequiredCliBinarySha256).Returns(Sha256(executable));
            var userSettings = new Mock<ISettingsProvider>();
            userSettings.Setup(x => x.ServerWorkerThreads).Returns(0);
            var (clientStream, serverStream) = FullDuplexStream.CreatePair();
            var process = new FakeIdeServerProcess(clientStream, clientStream);
            var serverRpc = new JsonRpc(new HeaderDelimitedMessageHandler(serverStream, serverStream));
            if (serverTarget != null)
            {
                serverRpc.AddLocalRpcTarget(serverTarget);
            }

            serverRpc.StartListening();
            var client = new IdeServerClient(
                settings.Object,
                new Mock<ILogger>().Object,
                new FakeProcessFactory(process),
                userSettings.Object,
                TimeSpan.FromSeconds(5));
            _ = Task.Run(async () =>
            {
                await Task.Yield();
                await serverRpc.NotifyWithParameterObjectAsync("cs-ide/start", startWire);
            });
            return new ContractClientHarness(client, serverRpc, serverStream, executable);
        }

        public void Dispose()
        {
            Client.Dispose();
            ServerRpc.Dispose();
            _serverStream.Dispose();
            if (System.IO.File.Exists(_executable))
            {
                System.IO.File.Delete(_executable);
            }
        }

        private static string Sha256(string path)
        {
            using var stream = System.IO.File.OpenRead(path);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
