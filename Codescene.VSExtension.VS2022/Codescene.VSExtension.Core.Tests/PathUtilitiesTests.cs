// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Util;

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
        public void IsInGitDirectory_FileInGitDirectory_ReturnsTrue()
        {
            var path = @"C:\repo\.git\config";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsTrue(result, "Files in .git directory should return true");
        }

        [TestMethod]
        public void IsInGitDirectory_FileInNestedGitDirectory_ReturnsTrue()
        {
            var path = @"C:\repo\.git\objects\pack\somefile";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsTrue(result, "Files deeply nested in .git directory should return true");
        }

        [TestMethod]
        public void IsInGitDirectory_GitHeadFile_ReturnsTrue()
        {
            var path = @"C:\repo\.git\HEAD";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsTrue(result, ".git/HEAD should return true");
        }

        [TestMethod]
        public void IsInGitDirectory_GitignoreFile_ReturnsFalse()
        {
            var path = @"C:\repo\.gitignore";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsFalse(result, ".gitignore should NOT be treated as being in .git directory");
        }

        [TestMethod]
        public void IsInGitDirectory_GitattributesFile_ReturnsFalse()
        {
            var path = @"C:\repo\.gitattributes";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsFalse(result, ".gitattributes should NOT be treated as being in .git directory");
        }

        [TestMethod]
        public void IsInGitDirectory_GitmodulesFile_ReturnsFalse()
        {
            var path = @"C:\repo\.gitmodules";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsFalse(result, ".gitmodules should NOT be treated as being in .git directory");
        }

        [TestMethod]
        public void IsInGitDirectory_GithubWorkflowFile_ReturnsFalse()
        {
            var path = @"C:\repo\.github\workflows\ci.yml";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsFalse(result, ".github/workflows/ci.yml should NOT be treated as being in .git directory");
        }

        [TestMethod]
        public void IsInGitDirectory_GitkeepFile_ReturnsFalse()
        {
            var path = @"C:\repo\empty_dir\.gitkeep";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsFalse(result, ".gitkeep should NOT be treated as being in .git directory");
        }

        [TestMethod]
        public void IsInGitDirectory_GithubActionsDirectory_ReturnsFalse()
        {
            var path = @"C:\repo\.github-actions\test.yml";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsFalse(result, "Files in .github-actions should NOT be treated as being in .git directory");
        }

        [TestMethod]
        public void IsInGitDirectory_RegularFile_ReturnsFalse()
        {
            var path = @"C:\repo\src\main.cs";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsFalse(result, "Regular files should return false");
        }

        [TestMethod]
        public void IsInGitDirectory_EmptyPath_ReturnsFalse()
        {
            var result = PathUtilities.IsInGitDirectory(string.Empty);

            Assert.IsFalse(result, "Empty path should return false");
        }

        [TestMethod]
        public void IsInGitDirectory_NullPath_ReturnsFalse()
        {
            var result = PathUtilities.IsInGitDirectory(null!);

            Assert.IsFalse(result, "Null path should return false");
        }

        [TestMethod]
        public void IsInGitDirectory_CaseInsensitive_ReturnsTrue()
        {
            var path = @"C:\repo\.GIT\config";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsTrue(result, ".GIT (uppercase) should be treated as .git directory");
        }

        [TestMethod]
        public void IsInGitDirectory_GitDirectoryAtEnd_ReturnsFalse()
        {
            var path = @"C:\repo\.git";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsFalse(result, "Path ending with .git (without trailing content) should return false");
        }

        [TestMethod]
        public void IsInGitDirectory_NestedGitignore_ReturnsFalse()
        {
            var path = @"C:\repo\subdir\.gitignore";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsFalse(result, "Nested .gitignore should NOT be treated as being in .git directory");
        }

        [TestMethod]
        public void IsInGitDirectory_DirectoryContainingGitInName_ReturnsFalse()
        {
            var path = @"C:\repo\my-git-tools\script.sh";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsFalse(result, "Directories containing 'git' in name should NOT be treated as .git directory");
        }

        [TestMethod]
        public void IsInGitDirectory_FileNameStartingWithGit_ReturnsFalse()
        {
            var path = @"C:\repo\gitconfig.txt";

            var result = PathUtilities.IsInGitDirectory(path);

            Assert.IsFalse(result, "Files starting with 'git' should NOT be treated as being in .git directory");
        }
    }
}
