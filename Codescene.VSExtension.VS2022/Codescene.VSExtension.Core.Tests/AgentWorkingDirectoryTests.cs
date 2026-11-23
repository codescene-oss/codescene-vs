// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using Codescene.VSExtension.Core.Application.Agent;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentWorkingDirectoryTests
    {
        [TestMethod]
        public void GitRoot_IsUsed()
        {
            var filePath = typeof(AgentWorkingDirectoryTests).Assembly.Location;

            var workDir = AgentWorkingDirectory.ForFile(filePath);

            Assert.IsNotNull(workDir);
            Assert.IsTrue(Directory.Exists(workDir));
            Assert.AreNotEqual(Path.GetDirectoryName(filePath), workDir.TrimEnd(Path.DirectorySeparatorChar));
            var gitEntry = Path.Combine(workDir.TrimEnd(Path.DirectorySeparatorChar), ".git");
            Assert.IsTrue(Directory.Exists(gitEntry) || File.Exists(gitEntry));
        }

        [TestMethod]
        public void NoGitRoot_UsesDirOfFile()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var workDir = AgentWorkingDirectory.ForFile(tempFile);

                Assert.AreEqual(Path.GetDirectoryName(tempFile), workDir);
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
        [DataRow(null)]
        [DataRow("")]
        public void NoPath_ReturnsNull(string? path)
        {
            Assert.IsNull(AgentWorkingDirectory.ForFile(path));
        }
    }
}
