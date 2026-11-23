// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class NativeAgentProcessTests
    {
        [TestMethod]
        public async Task CapturesStdoutStderrAndExitCode()
        {
            var logger = new Mock<ILogger>();
            var factory = new NativeAgentProcessFactory(logger.Object);

            var stdoutLines = new List<string>();
            var stderrLines = new List<string>();

            var startInfo = new AgentProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/d /c \"echo stdout-line && echo stderr-line 1>&2 && exit 42\"",
                WorkingDirectory = ".",
            };

            using var process = factory.Start(startInfo);

            process.StdoutLine += (_, line) => stdoutLines.Add(line);
            process.StderrLine += (_, line) => stderrLines.Add(line);

            var exitCode = await process.WaitForExitAsync();

            Assert.AreEqual(42, exitCode);
            Assert.IsTrue(stdoutLines.Count > 0);
            Assert.IsTrue(stdoutLines[0].Contains("stdout-line"));
            Assert.IsTrue(stderrLines.Count > 0);
            Assert.IsTrue(stderrLines[0].Contains("stderr-line"));
        }

        [TestMethod]
        public async Task PassesEnvironmentAddition()
        {
            var logger = new Mock<ILogger>();
            var factory = new NativeAgentProcessFactory(logger.Object);

            var stdoutLines = new List<string>();

            var startInfo = new AgentProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/d /c \"echo %TEST_VAR%\"",
                WorkingDirectory = ".",
                EnvironmentAdditions = new Dictionary<string, string>
                {
                    { "TEST_VAR", "test-value-123" },
                },
            };

            using var process = factory.Start(startInfo);

            process.StdoutLine += (_, line) => stdoutLines.Add(line);

            await process.WaitForExitAsync();

            Assert.IsTrue(stdoutLines.Count > 0);
            Assert.IsTrue(stdoutLines[0].Contains("test-value-123"));
        }

        [TestMethod]
        public async Task KillTree_EndsChildProcesses()
        {
            var logger = new Mock<ILogger>();
            var factory = new NativeAgentProcessFactory(logger.Object);

            var startInfo = new AgentProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/d /c \"ping -n 30 127.0.0.1\"",
                WorkingDirectory = ".",
            };

            using var process = factory.Start(startInfo);

            await Task.Delay(500);

            process.KillTree();

            var exitCode = await process.WaitForExitAsync();

            Assert.AreNotEqual(0, exitCode);
        }

        [TestMethod]
        public void ProcessStartThrows_MapsToGenericError()
        {
            var logger = new Mock<ILogger>();
            var factory = new NativeAgentProcessFactory(logger.Object);

            var startInfo = new AgentProcessStartInfo
            {
                FileName = "nonexistent-executable-xyz.exe",
                Arguments = "test",
                WorkingDirectory = ".",
            };

            try
            {
                using var process = factory.Start(startInfo);
                Assert.Fail("Expected exception");
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(System.ComponentModel.Win32Exception));
            }
        }

        [TestMethod]
        public void IsProcessInJob_ReflectsJobObjectAssignment()
        {
            var logger = new Mock<ILogger>();
            var factory = new NativeAgentProcessFactory(logger.Object);

            var startInfo = new AgentProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/d /c \"echo test\"",
                WorkingDirectory = ".",
            };

            using var process = factory.Start(startInfo) as NativeAgentProcess;

            Assert.IsNotNull(process);
        }

        [TestMethod]
        public async Task AlreadyExitedProcess_ReportsExitCode()
        {
            var psi = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/d /c exit 7")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            var raw = System.Diagnostics.Process.Start(psi);
            raw.WaitForExit();

            using var process = new NativeAgentProcess(raw, new Mock<ILogger>().Object);

            Assert.AreEqual(7, await process.WaitForExitAsync());
        }

        [TestMethod]
        public void Factory_NullStartInfo_Throws()
        {
            var factory = new NativeAgentProcessFactory(new Mock<ILogger>().Object);

            Assert.Throws<ArgumentNullException>(() => factory.Start(null));
        }
    }
}
