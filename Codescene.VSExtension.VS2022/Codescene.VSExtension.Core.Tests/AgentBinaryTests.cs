// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentBinaryTests
    {
        [TestMethod]
        public void Missing_FailsWithMessage()
        {
            var settings = new Mock<ICliSettingsProvider>();
            settings.Setup(s => s.AgentFileFullPath).Returns("C:\\nonexistent\\cs-agent.exe");

            var logger = new Mock<Interfaces.ILogger>();

            try
            {
                AgentBinary.Verify(settings.Object, logger.Object);
                Assert.Fail("Expected AgentFailure");
            }
            catch (AgentFailure ex)
            {
                Assert.AreEqual(AgentFailureKind.Generic, ex.Kind);
                Assert.IsTrue(ex.Message.Contains("not available for windows-x64"));
                Assert.IsTrue(ex.Message.Contains("missing"));
            }
        }

        [TestMethod]
        public void ShaMismatch_FailsWithMessage()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "test content");

                var settings = new Mock<ICliSettingsProvider>();
                settings.Setup(s => s.AgentFileFullPath).Returns(tempFile);
                settings.Setup(s => s.RequiredAgentBinarySha256).Returns("0000000000000000000000000000000000000000000000000000000000000000");

                var logger = new Mock<Interfaces.ILogger>();

                try
                {
                    AgentBinary.Verify(settings.Object, logger.Object);
                    Assert.Fail("Expected AgentFailure");
                }
                catch (AgentFailure ex)
                {
                    Assert.AreEqual(AgentFailureKind.Generic, ex.Kind);
                    Assert.IsTrue(ex.Message.Contains("failed integrity verification"));
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [TestMethod]
        public void Matching_Passes()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var content = "test content";
                File.WriteAllText(tempFile, content);

                using (var stream = File.OpenRead(tempFile))
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var hashBytes = sha.ComputeHash(stream);
                    var expectedSha = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();

                    var settings = new Mock<ICliSettingsProvider>();
                    settings.Setup(s => s.AgentFileFullPath).Returns(tempFile);
                    settings.Setup(s => s.RequiredAgentBinarySha256).Returns(expectedSha);

                    var logger = new Mock<Interfaces.ILogger>();

                    AgentBinary.Verify(settings.Object, logger.Object);
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }
    }
}
