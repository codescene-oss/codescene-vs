// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Enums.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class FileChangeEventTests
    {
        [TestMethod]
        public void Constructor_InitializesType()
        {
            var fileEvent = new FileChangeEvent(FileChangeType.Create, "test.cs");

            Assert.AreEqual(FileChangeType.Create, fileEvent.Type);
        }

        [TestMethod]
        public void Constructor_InitializesFilePath()
        {
            var filePath = "C:\\test\\file.cs";
            var fileEvent = new FileChangeEvent(FileChangeType.Change, filePath);

            Assert.AreEqual(filePath, fileEvent.FilePath);
        }

        [TestMethod]
        public void Constructor_WithCreateType_StoresCorrectly()
        {
            var fileEvent = new FileChangeEvent(FileChangeType.Create, "newfile.cs");

            Assert.AreEqual(FileChangeType.Create, fileEvent.Type);
            Assert.AreEqual("newfile.cs", fileEvent.FilePath);
        }

        [TestMethod]
        public void Constructor_WithChangeType_StoresCorrectly()
        {
            var fileEvent = new FileChangeEvent(FileChangeType.Change, "modified.cs");

            Assert.AreEqual(FileChangeType.Change, fileEvent.Type);
            Assert.AreEqual("modified.cs", fileEvent.FilePath);
        }

        [TestMethod]
        public void Constructor_WithDeleteType_StoresCorrectly()
        {
            var fileEvent = new FileChangeEvent(FileChangeType.Delete, "deleted.cs");

            Assert.AreEqual(FileChangeType.Delete, fileEvent.Type);
            Assert.AreEqual("deleted.cs", fileEvent.FilePath);
        }

        [TestMethod]
        public void Constructor_WithAbsolutePath_StoresCorrectly()
        {
            var absolutePath = Path.Combine("C:", "Users", "test", "file.cs");
            var fileEvent = new FileChangeEvent(FileChangeType.Change, absolutePath);

            Assert.AreEqual(absolutePath, fileEvent.FilePath);
        }

        [TestMethod]
        public void Constructor_WithRelativePath_StoresCorrectly()
        {
            var relativePath = "src/test.cs";
            var fileEvent = new FileChangeEvent(FileChangeType.Create, relativePath);

            Assert.AreEqual(relativePath, fileEvent.FilePath);
        }

        [TestMethod]
        public void Constructor_WithPathWithoutExtension_StoresCorrectly()
        {
            var pathWithoutExtension = "mydir";
            var fileEvent = new FileChangeEvent(FileChangeType.Delete, pathWithoutExtension);

            Assert.AreEqual(pathWithoutExtension, fileEvent.FilePath);
        }

        [TestMethod]
        public void Type_ReturnsInitializedValue()
        {
            var fileEvent = new FileChangeEvent(FileChangeType.Change, "test.cs");

            var type = fileEvent.Type;

            Assert.AreEqual(FileChangeType.Change, type);
        }

        [TestMethod]
        public void FilePath_ReturnsInitializedValue()
        {
            var expectedPath = "C:\\test\\file.cs";
            var fileEvent = new FileChangeEvent(FileChangeType.Create, expectedPath);

            var actualPath = fileEvent.FilePath;

            Assert.AreEqual(expectedPath, actualPath);
        }

        [TestMethod]
        public void Constructor_WithEmptyString_StoresEmptyString()
        {
            var fileEvent = new FileChangeEvent(FileChangeType.Create, string.Empty);

            Assert.AreEqual(string.Empty, fileEvent.FilePath);
        }

        [TestMethod]
        public void Constructor_WithPathContainingSpaces_StoresCorrectly()
        {
            var pathWithSpaces = "C:\\My Documents\\test file.cs";
            var fileEvent = new FileChangeEvent(FileChangeType.Change, pathWithSpaces);

            Assert.AreEqual(pathWithSpaces, fileEvent.FilePath);
        }

        [TestMethod]
        public void Constructor_WithUnixStylePath_StoresCorrectly()
        {
            var unixPath = "/home/user/test.cs";
            var fileEvent = new FileChangeEvent(FileChangeType.Create, unixPath);

            Assert.AreEqual(unixPath, fileEvent.FilePath);
        }

        [TestMethod]
        public void Constructor_WithMultipleExtensions_StoresCorrectly()
        {
            var multiExtPath = "archive.tar.gz";
            var fileEvent = new FileChangeEvent(FileChangeType.Delete, multiExtPath);

            Assert.AreEqual(multiExtPath, fileEvent.FilePath);
        }
    }
}
