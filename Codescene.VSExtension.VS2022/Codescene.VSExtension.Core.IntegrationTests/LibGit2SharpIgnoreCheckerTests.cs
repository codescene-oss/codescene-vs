// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.VS2022.Application.Git;
using LibGit2Sharp;
using Moq;

namespace Codescene.VSExtension.Core.IntegrationTests
{
    [TestClass]
    public class LibGit2SharpIgnoreCheckerTests
    {
        private string _testRepoPath = null!;
        private LibGit2SharpIgnoreChecker _checker = null!;

        [TestInitialize]
        public void Setup()
        {
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-git-repo-ignore-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testRepoPath);
            Repository.Init(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Config.Set("user.email", "test@example.com");
                repo.Config.Set("user.name", "Test User");
            }

            _checker = new LibGit2SharpIgnoreChecker(new Mock<ILogger>().Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testRepoPath))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(_testRepoPath, "*", SearchOption.AllDirectories))
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }

                    Directory.Delete(_testRepoPath, true);
                }
                catch
                {
                }
            }
        }

        [TestMethod]
        public void IsPathIgnored_UntrackedFileMatchingGitignore_ReturnsTrue()
        {
            File.WriteAllText(Path.Combine(_testRepoPath, ".gitignore"), "*.log\n");
            var ignoredPath = Path.Combine(_testRepoPath, "app.log");
            File.WriteAllText(ignoredPath, "content");

            Assert.IsTrue(_checker.IsPathIgnored(ignoredPath));
        }

        [TestMethod]
        public void IsPathIgnored_TrackedFileMatchingGitignore_ReturnsFalse()
        {
            var trackedPath = Path.Combine(_testRepoPath, "app.log");
            File.WriteAllText(trackedPath, "content");

            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, "app.log");
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit("Track log file", signature, signature);
            }

            File.WriteAllText(Path.Combine(_testRepoPath, ".gitignore"), "*.log\n");

            Assert.IsFalse(_checker.IsPathIgnored(trackedPath));
        }
    }
}
