// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class PathUtilitiesTests
    {
        [TestMethod]
        [DataRow(null, @"C:\test\file.txt", @"C:\test\file.txt")]
        [DataRow("", @"C:\test\file.txt", @"C:\test\file.txt")]
        [DataRow("http://", @"C:\test\file.txt", @"C:\test\file.txt")]
        [DataRow(@"C:\test", "not-a-valid-path-with-<>|", "not-a-valid-path-with-<>|")]
        [DataRow(@"relative\path", @"C:\test\file.txt", @"C:\test\file.txt")]
        public void GetRelativePath_InvalidInputs_ReturnsFullPath(string basePath, string fullPath, string expected)
        {
            var result = PathUtilities.GetRelativePath(basePath, fullPath);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        [DataRow(@"C:\test", null, null)]
        [DataRow(@"C:\test", "", "")]
        public void GetRelativePath_NullOrEmptyFullPath_ReturnsInput(string basePath, string fullPath, string expected)
        {
            var result = PathUtilities.GetRelativePath(basePath, fullPath);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void GetRelativePath_ValidPaths_ReturnsRelativePath()
        {
            var basePath = @"C:\test\project";
            var fullPath = @"C:\test\project\src\file.txt";
            var result = PathUtilities.GetRelativePath(basePath, fullPath);
            Assert.Contains("src", result);
            Assert.Contains("file.txt", result);
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
            Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), result);
            Assert.AreEqual(pathWithoutSeparator + Path.DirectorySeparatorChar, result);
        }

        [TestMethod]
        [DataRow(@"C:\repo\.git\config", "Files in .git directory should return true")]
        [DataRow(@"C:\repo\.git\objects\pack\somefile", "Files deeply nested in .git directory should return true")]
        [DataRow(@"C:\repo\.git\HEAD", ".git/HEAD should return true")]
        [DataRow(@"C:\repo\.GIT\config", ".GIT (uppercase) should be treated as .git directory")]
        public void IsInGitDirectory_PathsInGitDirectory_ReturnsTrue(string path, string description)
        {
            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsTrue(result, description);
        }

        [TestMethod]
        [DataRow(@"C:\repo\.gitignore", ".gitignore should NOT be treated as being in .git directory")]
        [DataRow(@"C:\repo\.gitattributes", ".gitattributes should NOT be treated as being in .git directory")]
        [DataRow(@"C:\repo\.gitmodules", ".gitmodules should NOT be treated as being in .git directory")]
        [DataRow(@"C:\repo\.github\workflows\ci.yml", ".github/workflows/ci.yml should NOT be treated as being in .git directory")]
        [DataRow(@"C:\repo\empty_dir\.gitkeep", ".gitkeep should NOT be treated as being in .git directory")]
        [DataRow(@"C:\repo\.github-actions\test.yml", "Files in .github-actions should NOT be treated as being in .git directory")]
        [DataRow(@"C:\repo\src\main.cs", "Regular files should return false")]
        [DataRow("", "Empty path should return false")]
        [DataRow(null, "Null path should return false")]
        [DataRow(@"C:\repo\.git", "Path ending with .git (without trailing content) should return false")]
        [DataRow(@"C:\repo\subdir\.gitignore", "Nested .gitignore should NOT be treated as being in .git directory")]
        [DataRow(@"C:\repo\my-git-tools\script.sh", "Directories containing 'git' in name should NOT be treated as .git directory")]
        [DataRow(@"C:\repo\gitconfig.txt", "Files starting with 'git' should NOT be treated as being in .git directory")]
        public void IsInGitDirectory_PathsNotInGitDirectory_ReturnsFalse(string path, string description)
        {
            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsFalse(result, description);
        }
    }
}
