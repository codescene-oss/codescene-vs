// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentSkillRunnerTests
    {
        private const string ValidOutputJson = "{\"task_id\":\"test\"}";

        private string _tempAgentBinary;
        private Mock<ISettingsProvider> _settings;
        private Mock<ICliSettingsProvider> _cliSettings;
        private Mock<IProviderKeyStore> _keyStore;
        private Mock<IIdeServerAuthClient> _authClient;
        private Mock<IAuthService> _authService;
        private Mock<IAgentProcessTracker> _tracker;
        private Mock<ILogger> _logger;

        [TestInitialize]
        public void Setup()
        {
            _tempAgentBinary = Path.GetTempFileName();
            File.WriteAllText(_tempAgentBinary, "test content");

            using (var stream = File.OpenRead(_tempAgentBinary))
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha.ComputeHash(stream);
                var expectedSha = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();

                _cliSettings = new Mock<ICliSettingsProvider>();
                _cliSettings.Setup(s => s.AgentFileFullPath).Returns(_tempAgentBinary);
                _cliSettings.Setup(s => s.RequiredAgentBinarySha256).Returns(expectedSha);
            }

            _settings = new Mock<ISettingsProvider>();
            _settings.Setup(s => s.AuthToken).Returns("pat-token");
            _settings.Setup(s => s.AgentModel).Returns("anthropic/claude-sonnet-5-5");
            _settings.Setup(s => s.AgentModelOverride).Returns(string.Empty);
            _settings.Setup(s => s.BedrockProfile).Returns(string.Empty);
            _settings.Setup(s => s.BedrockRegion).Returns(string.Empty);

            _keyStore = new Mock<IProviderKeyStore>();
            _keyStore.Setup(s => s.GetAll()).Returns(new Dictionary<string, string>());

            _authClient = new Mock<IIdeServerAuthClient>();
            _authService = new Mock<IAuthService>();
            _authService.Setup(a => a.Current).Returns(AuthSnapshot.Inactive);
            _tracker = new Mock<IAgentProcessTracker>();
            _logger = new Mock<ILogger>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_tempAgentBinary))
            {
                File.Delete(_tempAgentBinary);
            }
        }

        [TestMethod]
        public async Task WritesInputAndReadsOutput()
        {
            var output = new AgentOutput { TaskId = "task-123", FixResult = "fix_proposed" };
            string? inputJson = null;
            string? ioDir = null;
            var process = ProcessWritingOutput(JsonConvert.SerializeObject(output));
            var writeOutput = process.BeforeExit;
            process.BeforeExit = startInfo =>
            {
                ioDir = IoDirOf(startInfo);
                inputJson = File.ReadAllText(Path.Combine(ioDir, AgentSkill.RenderCodeFix.InputFile));
                writeOutput(startInfo);
            };

            var result = await RunAsync(new FakeAgentProcessFactory(process));

            Assert.AreEqual("task-123", result.TaskId);
            Assert.AreEqual("value", (string)JObject.Parse(inputJson)["test"]);
            Assert.IsFalse(Directory.Exists(ioDir), "I/O directory should be deleted");
        }

        [TestMethod]
        public async Task PublicRun_SignedInWithoutPat_UsesAuthServiceSnapshotForToken()
        {
            _settings.Setup(s => s.AuthToken).Returns(string.Empty);
            _authService.Setup(a => a.Current).Returns(AuthSnapshot.Inactive
                .WithActive(true)
                .WithStatus(new JObject { ["state"] = "signed-in" }));
            _authClient.Setup(a => a.AuthTokenAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new JObject { ["access-token"] = "oauth-token" });
            var factory = new FakeAgentProcessFactory(ProcessWritingOutput(ValidOutputJson));

            await CreateRunner(factory).RunAsync<AgentOutput>(
                AgentSkill.RenderCodeFix,
                Path.GetTempPath(),
                new { test = "value" },
                null,
                CancellationToken.None);

            var config = JObject.Parse(factory.LastStartInfo.EnvironmentAdditions["CS_AGENT_CONFIG"]);
            Assert.AreEqual("oauth-token", (string)config["codescene_access_token"]);
        }

        [TestMethod]
        public async Task ArgsAndConfigEnv_AreCorrect()
        {
            var factory = new FakeAgentProcessFactory(ProcessWritingOutput(ValidOutputJson));

            await RunAsync(factory, "/work/dir");

            var startInfo = factory.LastStartInfo;
            Assert.IsNotNull(startInfo);
            Assert.AreEqual(_tempAgentBinary, startInfo.FileName);
            Assert.AreEqual("run skill:render-code-fix --model anthropic/claude-sonnet-5-5", startInfo.Arguments);
            Assert.AreEqual("/work/dir", startInfo.WorkingDirectory);
            Assert.IsNotNull(startInfo.EnvironmentAdditions);
            var config = JObject.Parse(startInfo.EnvironmentAdditions["CS_AGENT_CONFIG"]);
            Assert.AreEqual("pat-token", (string)config["codescene_access_token"]);
            Assert.AreEqual("render-code-fix-reporter", (string)config.SelectToken("plugins[0]"));
            Assert.IsFalse(string.IsNullOrEmpty((string)config["io_json_dir"]));
        }

        [TestMethod]
        public async Task SecretsNeverInArgsOrIoDir()
        {
            _keyStore.Setup(s => s.GetAll()).Returns(new Dictionary<string, string>
            {
                ["anthropic"] = "secret-key-123",
            });

            var ioDirContents = new List<string>();
            var process = ProcessWritingOutput(ValidOutputJson);
            var writeOutput = process.BeforeExit;
            process.BeforeExit = startInfo =>
            {
                ioDirContents.AddRange(Directory.GetFiles(IoDirOf(startInfo)).Select(File.ReadAllText));
                writeOutput(startInfo);
            };
            var factory = new FakeAgentProcessFactory(process);

            await RunAsync(factory);

            var started = factory.LastStartInfo;
            Assert.IsFalse(started.Arguments.Contains("secret-key-123"));
            Assert.IsFalse(started.Arguments.Contains("pat-token"));
            Assert.IsFalse(started.WorkingDirectory.Contains("secret"));
            Assert.AreEqual(1, ioDirContents.Count);
            Assert.IsFalse(ioDirContents.Any(c => c.Contains("secret-key-123") || c.Contains("pat-token")));
        }

        [TestMethod]
        public async Task NoToken_FailsAuth_NoSpawn()
        {
            _settings.Setup(s => s.AuthToken).Returns(string.Empty);
            var factory = new FakeAgentProcessFactory(ProcessWritingOutput(ValidOutputJson));

            var ex = await RunExpectingFailureAsync(factory);

            Assert.AreEqual(AgentFailureKind.Auth, ex.Kind);
            Assert.IsTrue(ex.Message.Contains("Sign in"));
            Assert.AreEqual(0, factory.StartCount);
        }

        [TestMethod]
        public async Task IntegrityFailure_NoSpawn()
        {
            _cliSettings.Setup(s => s.RequiredAgentBinarySha256).Returns("0000000000000000000000000000000000000000000000000000000000000000");
            var factory = new FakeAgentProcessFactory(ProcessWritingOutput(ValidOutputJson));

            var ex = await RunExpectingFailureAsync(factory);

            Assert.AreEqual(AgentFailureKind.Generic, ex.Kind);
            Assert.IsTrue(ex.Message.Contains("integrity verification"));
            Assert.AreEqual(0, factory.StartCount);
        }

        [TestMethod]
        public async Task NonZeroExit_FailsGenericLogsRedactedStderr()
        {
            var stderr = new List<string> { "Error: Bearer token123 failed" };
            var process = new FakeAgentProcess(new List<string>(), stderr, 1);

            var ex = await RunExpectingFailureAsync(new FakeAgentProcessFactory(process));

            Assert.AreEqual(AgentFailureKind.Generic, ex.Kind);
            Assert.IsTrue(ex.Message.Contains("exited with code 1"));
            Assert.IsFalse(ex.Message.Contains("token123"));
            _logger.Verify(l => l.Warn(It.Is<string>(m => m.Contains("exited with code 1") && !m.Contains("token123")), It.IsAny<bool>()));
        }

        [TestMethod]
        public async Task MissingOutput_LogsLastFiveStderrLines()
        {
            var stderr = new List<string> { "line1", "line2", "line3", "line4", "line5", "line6" };
            var process = new FakeAgentProcess(new List<string>(), stderr, 0);

            var ex = await RunExpectingFailureAsync(new FakeAgentProcessFactory(process));

            Assert.AreEqual(AgentFailureKind.Generic, ex.Kind);
            Assert.IsTrue(ex.Message.Contains("did not produce the expected output file"));
            _logger.Verify(l => l.Warn(
                It.Is<string>(m =>
                    m.Contains("line2") && m.Contains("line3") && m.Contains("line4") &&
                    m.Contains("line5") && m.Contains("line6") && !m.Contains("line1")),
                It.IsAny<bool>()));
        }

        [TestMethod]
        public async Task MalformedOutputJson_FailsGeneric()
        {
            var factory = new FakeAgentProcessFactory(ProcessWritingOutput("not valid json"));

            var ex = await RunExpectingFailureAsync(factory);

            Assert.AreEqual(AgentFailureKind.Generic, ex.Kind);
            Assert.IsTrue(ex.Message.Contains("Failed to parse agent output"));
        }

        [TestMethod]
        public async Task IoDir_DeletedOnSuccessAndFailure()
        {
            string? successIoDir = null;
            var success = ProcessWritingOutput(ValidOutputJson);
            var writeOutput = success.BeforeExit;
            success.BeforeExit = startInfo =>
            {
                successIoDir = IoDirOf(startInfo);
                writeOutput(startInfo);
            };

            string? failureIoDir = null;
            var failure = new FakeAgentProcess(new List<string>(), new List<string> { "boom" }, 1)
            {
                BeforeExit = startInfo => failureIoDir = IoDirOf(startInfo),
            };

            await RunAsync(new FakeAgentProcessFactory(success));
            await RunExpectingFailureAsync(new FakeAgentProcessFactory(failure));

            Assert.IsNotNull(successIoDir);
            Assert.IsFalse(Directory.Exists(successIoDir));
            Assert.IsNotNull(failureIoDir);
            Assert.IsFalse(Directory.Exists(failureIoDir));
        }

        [TestMethod]
        public async Task Progress_IsReportedFromStdout()
        {
            var process = ProcessWritingOutput(ValidOutputJson, new List<string> { "Analysing", "Refactoring" });
            var progress = new List<string>();

            await CreateRunner(new FakeAgentProcessFactory(process)).RunAsync<AgentOutput>(
                AgentSkill.RenderCodeFix,
                Path.GetTempPath(),
                new { test = "value" },
                progress.Add,
                null,
                CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "Analysing", "Refactoring" }, progress);
        }

        [TestMethod]
        public async Task Run_TracksTheAgentProcess()
        {
            var process = ProcessWritingOutput(ValidOutputJson);

            await RunAsync(new FakeAgentProcessFactory(process));

            _tracker.Verify(t => t.Track(process), Times.Once);
        }

        [TestMethod]
        public async Task NullSkill_Throws_NoSpawn()
        {
            var factory = new FakeAgentProcessFactory(ProcessWritingOutput(ValidOutputJson));

            await Assert.ThrowsAsync<ArgumentNullException>(() => CreateRunner(factory).RunAsync<AgentOutput>(
                null,
                Path.GetTempPath(),
                null,
                null,
                null,
                CancellationToken.None));

            Assert.AreEqual(0, factory.StartCount);
        }

        [TestMethod]
        public async Task SkillWithoutIoFiles_ReturnsNull_AndSendsNoIoDir()
        {
            var factory = new FakeAgentProcessFactory(new FakeAgentProcess(new List<string>(), new List<string>(), 0));

            var result = await CreateRunner(factory).RunAsync<AgentOutput>(
                AgentSkill.CheckConnectivity,
                null,
                null,
                null,
                null,
                CancellationToken.None);

            Assert.IsNull(result);
            Assert.AreEqual("run skill:check-connectivity --model anthropic/claude-sonnet-5-5", factory.LastStartInfo.Arguments);
            Assert.AreEqual(Path.GetTempPath(), factory.LastStartInfo.WorkingDirectory);
        }

        [TestMethod]
        public async Task IoDirDeleteFailure_IsLoggedNotThrown()
        {
            string? ioDir = null;
            FileStream? held = null;
            var process = ProcessWritingOutput(ValidOutputJson);
            var writeOutput = process.BeforeExit;
            process.BeforeExit = startInfo =>
            {
                writeOutput(startInfo);
                ioDir = IoDirOf(startInfo);
                held = new FileStream(Path.Combine(ioDir, "locked.txt"), FileMode.Create, FileAccess.Write, FileShare.None);
            };

            try
            {
                var result = await RunAsync(new FakeAgentProcessFactory(process));

                Assert.AreEqual("test", result.TaskId);
                _logger.Verify(l => l.Debug(It.Is<string>(m => m.StartsWith("Failed to delete agent I/O directory"))));
            }
            finally
            {
                held?.Dispose();
                if (ioDir != null && Directory.Exists(ioDir))
                {
                    Directory.Delete(ioDir, true);
                }
            }
        }

        private static string IoDirOf(AgentProcessStartInfo startInfo)
        {
            var ioDir = (string?)JObject.Parse(startInfo.EnvironmentAdditions["CS_AGENT_CONFIG"])["io_json_dir"];
            return ioDir ?? throw new InvalidOperationException("CS_AGENT_CONFIG has no io_json_dir");
        }

        private static FakeAgentProcess ProcessWritingOutput(string outputJson, List<string>? stdout = null)
        {
            return new FakeAgentProcess(stdout ?? new List<string>(), new List<string>(), 0)
            {
                BeforeExit = startInfo => File.WriteAllText(
                    Path.Combine(IoDirOf(startInfo), AgentSkill.RenderCodeFix.OutputFile),
                    outputJson,
                    Encoding.UTF8),
            };
        }

        private AgentSkillRunner CreateRunner(IAgentProcessFactory factory)
        {
            return new AgentSkillRunner(
                _settings.Object,
                _cliSettings.Object,
                _keyStore.Object,
                _authClient.Object,
                _authService.Object,
                factory,
                _tracker.Object,
                _logger.Object);
        }

        private Task<AgentOutput> RunAsync(IAgentProcessFactory factory, string? workDir = null)
        {
            return CreateRunner(factory).RunAsync<AgentOutput>(
                AgentSkill.RenderCodeFix,
                workDir ?? Path.GetTempPath(),
                new { test = "value" },
                null,
                null,
                CancellationToken.None);
        }

        private async Task<AgentFailure> RunExpectingFailureAsync(IAgentProcessFactory factory)
        {
            try
            {
                await RunAsync(factory);
            }
            catch (AgentFailure ex)
            {
                return ex;
            }

            Assert.Fail("Expected AgentFailure");
            return null!;
        }
    }
}
