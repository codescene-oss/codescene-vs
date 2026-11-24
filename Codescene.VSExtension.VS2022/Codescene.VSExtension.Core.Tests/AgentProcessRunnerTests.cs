// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Models.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentProcessRunnerTests
    {
        [TestMethod]
        public async Task StdoutLastNonEmptyLine_IsProgress_Redacted()
        {
            var stdout = new List<string> { "line1", string.Empty, "  ", "line with token sk-123", "final line" };
            var process = new FakeAgentProcess(stdout, new List<string>(), 0);
            var factory = new FakeAgentProcessFactory(process);
            var logger = new Mock<ILogger>();

            var runner = new AgentProcessRunner(factory, logger.Object);
            var progressValues = new List<string>();

            var startInfo = new AgentProcessStartInfo
            {
                FileName = "test.exe",
                Arguments = "arg1",
                WorkingDirectory = ".",
            };

            var result = await runner.RunAsync(
                startInfo,
                progressValues.Add,
                TimeSpan.FromSeconds(10),
                new[] { "sk-123" },
                CancellationToken.None);

            Assert.AreEqual("final line", progressValues.Last());
            Assert.AreEqual(3, progressValues.Count);
            Assert.IsFalse(progressValues.Any(p => p.Contains("sk-123")));
            Assert.AreEqual(0, result.ExitCode);
        }

        [TestMethod]
        public async Task Stderr_AnsiStrippedRedactedAndKept()
        {
            var stderr = new List<string> { "\x1B[31merror\x1B[0m", "Bearer token123" };
            var process = new FakeAgentProcess(new List<string>(), stderr, 0);
            var factory = new FakeAgentProcessFactory(process);
            var logger = new Mock<ILogger>();

            var runner = new AgentProcessRunner(factory, logger.Object);

            var startInfo = new AgentProcessStartInfo
            {
                FileName = "test.exe",
                Arguments = "arg1",
                WorkingDirectory = ".",
            };

            var result = await runner.RunAsync(
                startInfo,
                null,
                TimeSpan.FromSeconds(10),
                Array.Empty<string>(),
                CancellationToken.None);

            Assert.IsFalse(result.Stderr.Contains("\x1B"));
            Assert.IsTrue(result.Stderr.Contains("error"));
            Assert.IsTrue(result.Stderr.Contains("Bearer"));
        }

        [TestMethod]
        public async Task Cancel_KillsTreeAndThrowsCancelled()
        {
            var process = new FakeAgentProcess(new List<string>(), new List<string>(), 0, exitsOnItsOwn: false);
            var factory = new FakeAgentProcessFactory(process);
            var logger = new Mock<ILogger>();

            var runner = new AgentProcessRunner(factory, logger.Object);
            var cts = new CancellationTokenSource();

            var startInfo = new AgentProcessStartInfo
            {
                FileName = "test.exe",
                Arguments = "arg1",
                WorkingDirectory = ".",
            };

            var task = runner.RunAsync(
                startInfo,
                null,
                TimeSpan.FromSeconds(10),
                Array.Empty<string>(),
                cts.Token);

            cts.Cancel();

            try
            {
                await task;
                Assert.Fail("Expected AgentFailure");
            }
            catch (AgentFailure ex)
            {
                Assert.AreEqual(AgentFailureKind.Cancelled, ex.Kind);
                Assert.IsTrue(process.WasKilled);
            }
        }

        [TestMethod]
        public async Task Timeout_KillsTreeAndThrowsGenericWithMessage()
        {
            var process = new FakeAgentProcess(new List<string>(), new List<string>(), 0, exitsOnItsOwn: false);
            var factory = new FakeAgentProcessFactory(process);
            var logger = new Mock<ILogger>();

            var runner = new AgentProcessRunner(factory, logger.Object);

            var startInfo = new AgentProcessStartInfo
            {
                FileName = "test.exe",
                Arguments = "arg1",
                WorkingDirectory = ".",
            };

            try
            {
                await runner.RunAsync(
                    startInfo,
                    null,
                    TimeSpan.FromMilliseconds(10),
                    Array.Empty<string>(),
                    CancellationToken.None);
                Assert.Fail("Expected AgentFailure");
            }
            catch (AgentFailure ex)
            {
                Assert.AreEqual(AgentFailureKind.Generic, ex.Kind);
                Assert.IsTrue(ex.Message.Contains("did not finish within"));
                Assert.IsTrue(ex.Message.Contains("minutes"));
                Assert.IsTrue(process.WasKilled);
            }
        }

        [TestMethod]
        public async Task SpawnLog_ContainsArgumentsOnly()
        {
            var process = new FakeAgentProcess(new List<string>(), new List<string>(), 0);
            var factory = new FakeAgentProcessFactory(process);
            var logger = new Mock<ILogger>();
            var loggedMessages = new List<string>();

            logger.Setup(l => l.Debug(It.IsAny<string>()))
                .Callback<string>(msg => loggedMessages.Add(msg));

            var runner = new AgentProcessRunner(factory, logger.Object);

            var startInfo = new AgentProcessStartInfo
            {
                FileName = "test.exe",
                Arguments = "arg1 arg2",
                WorkingDirectory = "/work",
                EnvironmentAdditions = new Dictionary<string, string> { { "SECRET", "value" } },
            };

            await runner.RunAsync(
                startInfo,
                null,
                TimeSpan.FromSeconds(10),
                Array.Empty<string>(),
                CancellationToken.None);

            var spawnLog = loggedMessages.FirstOrDefault(m => m.Contains("Starting cs-agent"));
            Assert.IsNotNull(spawnLog);
            Assert.IsTrue(spawnLog.Contains("arg1 arg2"));
            Assert.IsFalse(spawnLog.Contains("/work"));
            Assert.IsFalse(spawnLog.Contains("SECRET"));
            Assert.IsFalse(spawnLog.Contains("value"));
        }

        [TestMethod]
        public async Task Run_TracksProcessWhileItRuns()
        {
            var process = new FakeAgentProcess(new List<string>(), new List<string>(), 0);
            var factory = new FakeAgentProcessFactory(process);
            var tracker = new Mock<IAgentProcessTracker>();
            var runner = new AgentProcessRunner(factory, new Mock<ILogger>().Object, tracker.Object);

            var startInfo = new AgentProcessStartInfo
            {
                FileName = "test.exe",
                Arguments = "arg1",
                WorkingDirectory = ".",
            };

            await runner.RunAsync(startInfo, null, TimeSpan.FromSeconds(10), Array.Empty<string>(), CancellationToken.None);

            tracker.Verify(t => t.Track(process), Times.Once);
        }

        [TestMethod]
        public async Task Tracker_ForgetsExitedProcess()
        {
            var tracker = new AgentProcessTracker();
            var process = new FakeAgentProcess(new List<string>(), new List<string>(), 0);

            tracker.Track(process);
            process.Start();

            await process.WaitForExitAsync();

            tracker.KillAll();
            Assert.IsFalse(process.WasKilled);
        }

        [TestMethod]
        public void KillAll_KillsLiveProcesses()
        {
            var tracker = new AgentProcessTracker();
            var process1 = new FakeAgentProcess(new List<string>(), new List<string>(), 0, exitsOnItsOwn: false);
            var process2 = new FakeAgentProcess(new List<string>(), new List<string>(), 0, exitsOnItsOwn: false);

            tracker.Track(process1);
            tracker.Track(process2);

            tracker.KillAll();

            Assert.IsTrue(process1.WasKilled);
            Assert.IsTrue(process2.WasKilled);
        }

        [TestMethod]
        public async Task NullStartInfo_Throws()
        {
            var runner = new AgentProcessRunner(new Mock<IAgentProcessFactory>().Object, new Mock<ILogger>().Object);

            await Assert.ThrowsAsync<ArgumentNullException>(() => runner.RunAsync(null, null, TimeSpan.FromSeconds(1), null, CancellationToken.None));
        }

        [TestMethod]
        public async Task StartFailsAfterCancellation_ReportsCancelled()
        {
            var factory = new Mock<IAgentProcessFactory>();
            factory.Setup(f => f.Start(It.IsAny<AgentProcessStartInfo>())).Throws(new InvalidOperationException("start failed"));
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var runner = new AgentProcessRunner(factory.Object, new Mock<ILogger>().Object);

            var ex = await Assert.ThrowsAsync<AgentFailure>(() => runner.RunAsync(new AgentProcessStartInfo { FileName = "test.exe" }, null, TimeSpan.FromSeconds(1), null, cts.Token));

            Assert.AreEqual(AgentFailureKind.Cancelled, ex.Kind);
        }

        [TestMethod]
        public void Tracker_KillAll_SkipsNullAndSurvivesFailingProcess()
        {
            var tracker = new AgentProcessTracker();
            var failing = new Mock<IAgentProcess>();
            failing.Setup(p => p.WaitForExitAsync()).Returns(new TaskCompletionSource<int>().Task);
            failing.Setup(p => p.KillTree()).Throws(new InvalidOperationException("already gone"));
            var healthy = new FakeAgentProcess(new List<string>(), new List<string>(), 0, exitsOnItsOwn: false);

            tracker.Track(null);
            tracker.Track(failing.Object);
            tracker.Track(healthy);
            tracker.KillAll();

            Assert.IsTrue(healthy.WasKilled);
        }
    }
}
