// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentConnectivityTesterTests
    {
        [TestMethod]
        public async Task ExitZero_Success()
        {
            var skillRunner = new Mock<IAgentSkillRunner>();
            skillRunner.Setup(r => r.RunAsync<object>(
                It.Is<AgentSkill>(s => s.Name == "check-connectivity"),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult<object>(null));

            var authService = new Mock<IAuthService>();
            authService.Setup(a => a.Current).Returns(AuthSnapshot.Inactive);

            var logger = new Mock<ILogger>();

            var tester = new AgentConnectivityTester(skillRunner.Object, authService.Object, logger.Object);

            var result = await tester.TestAsync(Path.GetTempPath(), CancellationToken.None);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Cancelled);
            Assert.IsNull(result.Error);
        }

        [TestMethod]
        public async Task Failure_UsesRedactedStderr()
        {
            var skillRunner = new Mock<IAgentSkillRunner>();
            skillRunner.Setup(r => r.RunAsync<object>(
                It.IsAny<AgentSkill>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new AgentFailure(AgentFailureKind.Generic, "Connection failed"));

            var authService = new Mock<IAuthService>();
            authService.Setup(a => a.Current).Returns(AuthSnapshot.Inactive);

            var logger = new Mock<ILogger>();

            var tester = new AgentConnectivityTester(skillRunner.Object, authService.Object, logger.Object);

            var result = await tester.TestAsync(Path.GetTempPath(), CancellationToken.None);

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.Cancelled);
            Assert.AreEqual("Connection failed", result.Error);
        }

        [TestMethod]
        public async Task FailureWithoutStderr_UsesExitCode()
        {
            var skillRunner = new Mock<IAgentSkillRunner>();
            skillRunner.Setup(r => r.RunAsync<object>(
                It.IsAny<AgentSkill>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Process exited with code 1"));

            var authService = new Mock<IAuthService>();
            authService.Setup(a => a.Current).Returns(AuthSnapshot.Inactive);

            var logger = new Mock<ILogger>();

            var tester = new AgentConnectivityTester(skillRunner.Object, authService.Object, logger.Object);

            var result = await tester.TestAsync(Path.GetTempPath(), CancellationToken.None);

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Process exited with code 1", result.Error);
        }

        [TestMethod]
        public async Task Timeout_Message()
        {
            var skillRunner = new Mock<IAgentSkillRunner>();
            var tcs = new TaskCompletionSource<object>();
            skillRunner.Setup(r => r.RunAsync<object>(
                It.IsAny<AgentSkill>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()))
                .Returns(async (AgentSkill skill, string workDir, object input, Action<string> onProgress, CancellationToken ct) =>
                {
                    await Task.Delay(Timeout.Infinite, ct);
                    return await tcs.Task;
                });

            var authService = new Mock<IAuthService>();
            authService.Setup(a => a.Current).Returns(AuthSnapshot.Inactive);

            var logger = new Mock<ILogger>();

            var tester = new AgentConnectivityTester(skillRunner.Object, authService.Object, logger.Object)
            {
                Timeout = TimeSpan.FromMilliseconds(1),
            };

            var result = await tester.TestAsync(Path.GetTempPath(), CancellationToken.None);

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.Cancelled);
            Assert.AreEqual("Connectivity test timed out", result.Error);
        }

        [TestMethod]
        public async Task Cancel_ReturnsCancelled()
        {
            var skillRunner = new Mock<IAgentSkillRunner>();
            skillRunner.Setup(r => r.RunAsync<object>(
                It.IsAny<AgentSkill>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new OperationCanceledException());

            var authService = new Mock<IAuthService>();
            authService.Setup(a => a.Current).Returns(AuthSnapshot.Inactive);

            var logger = new Mock<ILogger>();

            var tester = new AgentConnectivityTester(skillRunner.Object, authService.Object, logger.Object);

            var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = await tester.TestAsync(Path.GetTempPath(), cts.Token);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Cancelled);
        }

        [TestMethod]
        public async Task FailureWithoutExitCode_UsesMinusOne()
        {
            var skillRunner = new Mock<IAgentSkillRunner>();
            skillRunner.Setup(r => r.RunAsync<object>(
                It.IsAny<AgentSkill>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));
            var authService = new Mock<IAuthService>();
            authService.Setup(a => a.Current).Returns(AuthSnapshot.Inactive);

            var tester = new AgentConnectivityTester(skillRunner.Object, authService.Object, new Mock<ILogger>().Object);
            var result = await tester.TestAsync(Path.GetTempPath(), CancellationToken.None);

            Assert.AreEqual("Process exited with code -1", result.Error);
        }

        [TestMethod]
        public async Task ConcreteRunner_RunsCheckConnectivityWithTheTestersSnapshot()
        {
            var binary = Path.GetTempFileName();
            try
            {
                var factory = new FakeAgentProcessFactory(new FakeAgentProcess(new List<string>(), new List<string>(), 0));
                var runner = CreateConcreteRunner(binary, factory);
                var authService = new Mock<IAuthService>();
                authService.Setup(a => a.Current).Returns(AuthSnapshot.Inactive.WithActive(true).WithStatus(new JObject { ["state"] = "signed-in" }));

                var tester = new AgentConnectivityTester(runner, authService.Object, new Mock<ILogger>().Object);
                var result = await tester.TestAsync("C:/work", CancellationToken.None);

                Assert.IsTrue(result.Success);
                Assert.AreEqual("run skill:check-connectivity --model anthropic/claude-sonnet-5-5", factory.LastStartInfo.Arguments);
                Assert.AreEqual("oauth-token", (string?)JObject.Parse(factory.LastStartInfo.EnvironmentAdditions["CS_AGENT_CONFIG"])["codescene_access_token"]);
            }
            finally
            {
                File.Delete(binary);
            }
        }

        private static AgentSkillRunner CreateConcreteRunner(string binary, FakeAgentProcessFactory factory)
        {
            string sha;
            using (var stream = File.OpenRead(binary))
            using (var hasher = System.Security.Cryptography.SHA256.Create())
            {
                sha = BitConverter.ToString(hasher.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }

            var cliSettings = new Mock<ICliSettingsProvider>();
            cliSettings.Setup(s => s.AgentFileFullPath).Returns(binary);
            cliSettings.Setup(s => s.RequiredAgentBinarySha256).Returns(sha);
            var settings = new Mock<ISettingsProvider>();
            settings.Setup(s => s.AuthToken).Returns(string.Empty);
            settings.Setup(s => s.AgentModel).Returns("anthropic/claude-sonnet-5-5");
            settings.Setup(s => s.AgentModelOverride).Returns(string.Empty);
            var keyStore = new Mock<IProviderKeyStore>();
            keyStore.Setup(k => k.GetAll()).Returns(new Dictionary<string, string>());
            var authClient = new Mock<IIdeServerAuthClient>();
            authClient.Setup(a => a.AuthTokenAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new JObject { ["access-token"] = "oauth-token" });

            return new AgentSkillRunner(
                settings.Object,
                cliSettings.Object,
                keyStore.Object,
                authClient.Object,
                null,
                factory,
                new Mock<IAgentProcessTracker>().Object,
                new Mock<ILogger>().Object);
        }
    }
}
