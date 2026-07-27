// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.VS2022.Application.Git;
using LibGit2Sharp;
using Moq;

namespace Codescene.VSExtension.Core.IntegrationTests
{
    [TestClass]
    public class GlobalGitignoreCacheBustingTests
    {
        private string _testRepoPath;
        private string _globalExcludesDir;
        private string _globalExcludesPath;
        private GitService _gitService;
        private Mock<ILogger> _mockLogger;

        [TestInitialize]
        public void Setup()
        {
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-git-repo-global-{Guid.NewGuid()}");
            _globalExcludesDir = Path.Combine(Path.GetTempPath(), $"test-global-excludes-{Guid.NewGuid()}");
            _globalExcludesPath = Path.Combine(_globalExcludesDir, "global-gitignore");

            Directory.CreateDirectory(_testRepoPath);
            Directory.CreateDirectory(_globalExcludesDir);

            Repository.Init(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Config.Set("user.email", "test@example.com");
                repo.Config.Set("user.name", "Test User");
            }

            File.WriteAllText(_globalExcludesPath, "# Global gitignore\n");

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Config.Set("core.excludesfile", _globalExcludesPath);
            }

            CommitFile("README.md", "# Test Repository", "Initial commit");

            _mockLogger = new Mock<ILogger>();
            _gitService = new GitService(_mockLogger.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _gitService?.Dispose();

            if (Directory.Exists(_testRepoPath))
            {
                try
                {
                    Directory.Delete(_testRepoPath, true);
                }
                catch
                {
                }
            }

            if (Directory.Exists(_globalExcludesDir))
            {
                try
                {
                    Directory.Delete(_globalExcludesDir, true);
                }
                catch
                {
                }
            }
        }

        [TestMethod]
        public async Task GlobalExcludesFileModified_InvalidatesCache()
        {
            var testFile = CreateFile("secret.txt", "secret content");

            Assert.IsFalse(_gitService.IsFileIgnored(testFile), "File should not be ignored initially");

            Assert.IsFalse(_gitService.IsFileIgnored(testFile), "Second call should use cache");

            File.WriteAllText(_globalExcludesPath, "# Global gitignore\nsecret.txt\n");

            await WaitForIgnoreStateAsync(testFile, expectedIgnored: true);
        }

        [TestMethod]
        public async Task GlobalExcludesFileDeleted_InvalidatesCache()
        {
            File.WriteAllText(_globalExcludesPath, "# Global gitignore\nignored.txt\n");

            var testFile = CreateFile("ignored.txt", "content");

            Assert.IsTrue(_gitService.IsFileIgnored(testFile), "File should be ignored initially");

            File.Delete(_globalExcludesPath);

            await WaitForIgnoreStateAsync(testFile, expectedIgnored: false);
        }

        [TestMethod]
        public async Task GlobalExcludesFileCreatedAfterInit_InvalidatesCache()
        {
            File.Delete(_globalExcludesPath);

            _gitService?.Dispose();
            _gitService = new GitService(_mockLogger.Object);

            var testFile = CreateFile("newignored.txt", "content");

            Assert.IsFalse(_gitService.IsFileIgnored(testFile), "File should not be ignored initially");

            File.WriteAllText(_globalExcludesPath, "# Global gitignore\nnewignored.txt\n");

            await WaitForIgnoreStateAsync(testFile, expectedIgnored: true);
        }

        [TestMethod]
        public void MissingGlobalExcludesFile_WatcherHandlesGracefully()
        {
            var nonexistentPath = Path.Combine(_globalExcludesDir, "nonexistent-excludes");
            using (var repo = new Repository(_testRepoPath))
            {
                repo.Config.Set("core.excludesfile", nonexistentPath);
            }

            _gitService?.Dispose();
            _gitService = new GitService(_mockLogger.Object);

            var testFile = CreateFile("test.txt", "content");

            Assert.IsFalse(_gitService.IsFileIgnored(testFile), "Should handle missing global excludes gracefully");
        }

        [TestMethod]
        public async Task GlobalExcludesFileRenamed_InvalidatesCache()
        {
            File.WriteAllText(_globalExcludesPath, "# Global gitignore\nrenamed.txt\n");

            var testFile = CreateFile("renamed.txt", "content");

            Assert.IsTrue(_gitService.IsFileIgnored(testFile), "File should be ignored initially");

            var renamedPath = _globalExcludesPath + ".bak";
            File.Move(_globalExcludesPath, renamedPath);

            await WaitForIgnoreStateAsync(testFile, expectedIgnored: false);

            File.Move(renamedPath, _globalExcludesPath);

            await WaitForIgnoreStateAsync(testFile, expectedIgnored: true);
        }

        [TestMethod]
        public async Task GlobalExcludesWithWildcard_InvalidatesCacheCorrectly()
        {
            var testFile1 = CreateFile("test1.log", "log content");
            var testFile2 = CreateFile("test2.log", "log content");
            var testFile3 = CreateFile("app.ts", "code");

            Assert.IsFalse(_gitService.IsFileIgnored(testFile1));
            Assert.IsFalse(_gitService.IsFileIgnored(testFile2));
            Assert.IsFalse(_gitService.IsFileIgnored(testFile3));

            File.WriteAllText(_globalExcludesPath, "# Global gitignore\n*.log\n");

            await WaitForIgnoreStateAsync(testFile1, expectedIgnored: true);

            Assert.IsTrue(_gitService.IsFileIgnored(testFile2), "*.log should be ignored");
            Assert.IsFalse(_gitService.IsFileIgnored(testFile3), "app.ts should not be ignored");
        }

        [TestMethod]
        public async Task GlobalExcludesPathWithSpaces_WatcherWorksCorrectly()
        {
            var spacedDir = Path.Combine(Path.GetTempPath(), $"test global excludes {Guid.NewGuid()}");
            var spacedExcludesPath = Path.Combine(spacedDir, "global gitignore file");

            try
            {
                Directory.CreateDirectory(spacedDir);
                File.WriteAllText(spacedExcludesPath, "# Global gitignore\n");

                using (var repo = new Repository(_testRepoPath))
                {
                    repo.Config.Set("core.excludesfile", spacedExcludesPath);
                }

                _gitService?.Dispose();
                _gitService = new GitService(_mockLogger.Object);

                var testFile = CreateFile("spacedtest.txt", "content");

                Assert.IsFalse(_gitService.IsFileIgnored(testFile), "File should not be ignored initially");

                File.WriteAllText(spacedExcludesPath, "# Global gitignore\nspacedtest.txt\n");

                await WaitForIgnoreStateAsync(testFile, expectedIgnored: true);
            }
            finally
            {
                if (Directory.Exists(spacedDir))
                {
                    try
                    {
                        Directory.Delete(spacedDir, true);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private string CreateFile(string filename, string content)
        {
            var filePath = Path.Combine(_testRepoPath, filename);
            File.WriteAllText(filePath, content);
            return filePath;
        }

        private void CommitFile(string filename, string content, string message)
        {
            var filePath = CreateFile(filename, content);

            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, filename);
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit(message, signature, signature);
            }
        }

        private async Task WaitForIgnoreStateAsync(string filePath, bool expectedIgnored, int timeoutMs = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (_gitService.IsFileIgnored(filePath) == expectedIgnored)
                {
                    return;
                }

                await Task.Delay(50);
            }

            Assert.Fail($"Timed out waiting for IsFileIgnored({Path.GetFileName(filePath)}) to become {expectedIgnored}");
        }
    }
}
