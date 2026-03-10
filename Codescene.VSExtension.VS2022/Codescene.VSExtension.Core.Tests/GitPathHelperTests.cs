// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitPathHelperTests
    {
        private string _tempDir;
        private string _workspaceDir;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GitPathHelperTests-" + Guid.NewGuid().ToString("N"));
            _workspaceDir = Path.Combine(_tempDir, "workspace");
            Directory.CreateDirectory(_workspaceDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch
                {
                }
            }
        }

        [TestMethod]
        public void IsFileInWorkspace_FileDoesNotExist_ReturnsFalse()
        {
            var result = GitPathHelper.IsFileInWorkspace("nonexistent.cs", _tempDir, _workspaceDir);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsFileInWorkspace_FileExistsInsideWorkspace_ReturnsTrue()
        {
            var filePath = Path.Combine(_workspaceDir, "sub", "file.cs");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, "x");
            var relativePath = "workspace" + Path.DirectorySeparatorChar + "sub" + Path.DirectorySeparatorChar + "file.cs";
            var result = GitPathHelper.IsFileInWorkspace(relativePath, _tempDir, _workspaceDir);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsFileInWorkspace_FileExistsAndEqualsWorkspace_ReturnsTrue()
        {
            var filePath = Path.Combine(_workspaceDir, "file.cs");
            File.WriteAllText(filePath, "x");
            var relativePath = "workspace" + Path.DirectorySeparatorChar + "file.cs";
            var result = GitPathHelper.IsFileInWorkspace(relativePath, _tempDir, _workspaceDir);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsFileInWorkspace_InvalidPath_ReturnsFalse()
        {
            var result = GitPathHelper.IsFileInWorkspace("\0bad", _tempDir, _workspaceDir);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ConvertToAbsolutePath_ValidPath_ReturnsFullPath()
        {
            var result = GitPathHelper.ConvertToAbsolutePath("sub\\file.cs", _tempDir);
            Assert.AreEqual(Path.GetFullPath(Path.Combine(_tempDir, "sub", "file.cs")), result);
        }
    }
}
