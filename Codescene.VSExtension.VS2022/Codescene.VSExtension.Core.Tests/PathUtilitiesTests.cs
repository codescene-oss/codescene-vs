// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using Codescene.VSExtension.Core.Application.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class PathUtilitiesTests
    {
        [TestMethod]
        public void GetRelativePath_NullBasePath_ReturnsFullPath()
        {
            var fullPath = @"C:\test\file.txt";
            var result = PathUtilities.GetRelativePath(null, fullPath);
            Assert.AreEqual(fullPath, result);
        }

        [TestMethod]
        public void GetRelativePath_EmptyBasePath_ReturnsFullPath()
        {
            var fullPath = @"C:\test\file.txt";
            var result = PathUtilities.GetRelativePath(string.Empty, fullPath);
            Assert.AreEqual(fullPath, result);
        }

        [TestMethod]
        public void GetRelativePath_NullFullPath_ReturnsNull()
        {
            var basePath = @"C:\test";
            var result = PathUtilities.GetRelativePath(basePath, null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetRelativePath_EmptyFullPath_ReturnsEmpty()
        {
            var basePath = @"C:\test";
            var result = PathUtilities.GetRelativePath(basePath, string.Empty);
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void GetRelativePath_InvalidBasePath_ReturnsFullPath()
        {
            var invalidBasePath = "http://";
            var fullPath = @"C:\test\file.txt";
            var result = PathUtilities.GetRelativePath(invalidBasePath, fullPath);
            Assert.AreEqual(fullPath, result);
        }

        [TestMethod]
        public void GetRelativePath_InvalidFullPath_ReturnsFullPath()
        {
            var basePath = @"C:\test";
            var invalidFullPath = "not-a-valid-path-with-<>|";
            var result = PathUtilities.GetRelativePath(basePath, invalidFullPath);
            Assert.AreEqual(invalidFullPath, result);
        }

        [TestMethod]
        public void GetRelativePath_RelativeBasePath_CatchesException()
        {
            var relativeBasePath = "relative\\path";
            var fullPath = @"C:\test\file.txt";
            var result = PathUtilities.GetRelativePath(relativeBasePath, fullPath);
            Assert.AreEqual(fullPath, result);
        }

        [TestMethod]
        public void GetRelativePath_ValidPaths_ReturnsRelativePath()
        {
            var basePath = @"C:\test\project";
            var fullPath = @"C:\test\project\src\file.txt";
            var result = PathUtilities.GetRelativePath(basePath, fullPath);
            Assert.IsTrue(result.Contains("src"));
            Assert.IsTrue(result.Contains("file.txt"));
        }

        [TestMethod]
        public void AppendDirectorySeparatorChar_PathAlreadyEndsWithSeparator_ReturnsUnchanged()
        {
            var pathWithSeparator = @"C:\test\";
            var result = PathUtilities.AppendDirectorySeparatorChar(pathWithSeparator);
            Assert.AreEqual(pathWithSeparator, result);
        }

        [TestMethod]
        public void AppendDirectorySeparatorChar_PathWithoutSeparator_AppendsSeparator()
        {
            var pathWithoutSeparator = @"C:\test";
            var result = PathUtilities.AppendDirectorySeparatorChar(pathWithoutSeparator);
            Assert.IsTrue(result.EndsWith(Path.DirectorySeparatorChar.ToString()));
            Assert.AreEqual(pathWithoutSeparator + Path.DirectorySeparatorChar, result);
        }
    }
}
