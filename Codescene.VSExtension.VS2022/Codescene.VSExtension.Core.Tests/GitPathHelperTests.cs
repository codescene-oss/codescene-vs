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

        [TestMethod]
        public void IsPathUnderAnyRoot_NullRoots_ReturnsTrue()
        {
            var result = GitPathHelper.IsPathUnderAnyRoot(Path.Combine(_workspaceDir, "file.cs"), null);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsPathUnderAnyRoot_EmptyRoots_ReturnsTrue()
        {
            var result = GitPathHelper.IsPathUnderAnyRoot(Path.Combine(_workspaceDir, "file.cs"), Array.Empty<string>());
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsPathUnderAnyRoot_InvalidFullPath_ReturnsFalse()
        {
            var result = GitPathHelper.IsPathUnderAnyRoot("\0invalid", new[] { _workspaceDir });
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsPathUnderAnyRoot_EmptyRootInCollection_SkipsAndChecksOthers()
        {
            var filePath = Path.Combine(_workspaceDir, "a.cs");
            File.WriteAllText(filePath, "x");
            var result = GitPathHelper.IsPathUnderAnyRoot(filePath, new[] { string.Empty, _workspaceDir });
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsPathUnderAnyRoot_NullFullPath_ReturnsFalse()
        {
            var result = GitPathHelper.IsPathUnderAnyRoot(null, new[] { _workspaceDir });
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsPathUnderAnyRoot_EmptyFullPath_ReturnsFalse()
        {
            var result = GitPathHelper.IsPathUnderAnyRoot(string.Empty, new[] { _workspaceDir });
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsFileInWorkspace_CollectionOverload_NullWorkspacePaths_ReturnsTrue()
        {
            var result = GitPathHelper.IsFileInWorkspace("file.cs", _tempDir, (IReadOnlyCollection<string>)null);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsFileInWorkspace_CollectionOverload_EmptyWorkspacePaths_ReturnsTrue()
        {
            var result = GitPathHelper.IsFileInWorkspace("file.cs", _tempDir, Array.Empty<string>());
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsFileInWorkspace_CollectionOverload_FileInOneWorkspace_ReturnsTrue()
        {
            var filePath = Path.Combine(_workspaceDir, "found.cs");
            File.WriteAllText(filePath, "x");
            var otherDir = Path.Combine(_tempDir, "other");
            Directory.CreateDirectory(otherDir);
            var relativePath = "workspace" + Path.DirectorySeparatorChar + "found.cs";
            var result = GitPathHelper.IsFileInWorkspace(relativePath, _tempDir, new[] { otherDir, _workspaceDir });
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsFileInWorkspace_CollectionOverload_FileInNoWorkspace_ReturnsFalse()
        {
            var otherDir1 = Path.Combine(_tempDir, "other1");
            var otherDir2 = Path.Combine(_tempDir, "other2");
            Directory.CreateDirectory(otherDir1);
            Directory.CreateDirectory(otherDir2);
            var result = GitPathHelper.IsFileInWorkspace("nonexistent.cs", _tempDir, new[] { otherDir1, otherDir2 });
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ConvertToAbsolutePath_InvalidPath_FallsBackToPathCombine()
        {
            var invalidRelative = "file*.cs";
            var result = GitPathHelper.ConvertToAbsolutePath(invalidRelative, _tempDir);
            Assert.AreEqual(Path.Combine(_tempDir, invalidRelative), result);
        }

        [TestMethod]
        public void IsFileInWorkspace_FileExistsOutsideWorkspace_ReturnsFalse()
        {
            var outsideDir = Path.Combine(_tempDir, "outside");
            Directory.CreateDirectory(outsideDir);
            var filePath = Path.Combine(outsideDir, "file.cs");
            File.WriteAllText(filePath, "x");
            var relativePath = "outside" + Path.DirectorySeparatorChar + "file.cs";
            var result = GitPathHelper.IsFileInWorkspace(relativePath, _tempDir, _workspaceDir);
            Assert.IsFalse(result);
        }
    }
}
