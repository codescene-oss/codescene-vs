// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitIgnoreSemanticsTests
    {
        private string _testRepoPath = null!;

        [TestInitialize]
        public void Setup()
        {
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-repo-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testRepoPath);
            Repository.Init(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Config.Set("user.email", "test@example.com");
                repo.Config.Set("user.name", "Test User");
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testRepoPath))
            {
                foreach (var file in Directory.GetFiles(_testRepoPath, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(_testRepoPath, true);
            }
        }

        [TestMethod]
        public void UntrackedFileMatchingGitignore_IsIgnored()
        {
            File.WriteAllText(Path.Combine(_testRepoPath, ".gitignore"), "*.log\n");
            File.WriteAllText(Path.Combine(_testRepoPath, "app.log"), "content");

            using (var repo = new Repository(_testRepoPath))
            {
                Assert.IsTrue(GitIgnoreSemantics.IsPathIgnoredConsideringIndex(repo, "app.log"));
            }
        }

        [TestMethod]
        public void TrackedFileMatchingGitignore_IsNotIgnored()
        {
            File.WriteAllText(Path.Combine(_testRepoPath, "app.log"), "content");

            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, "app.log");
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit("Track log file", signature, signature);
            }

            File.WriteAllText(Path.Combine(_testRepoPath, ".gitignore"), "*.log\n");

            using (var repo = new Repository(_testRepoPath))
            {
                Assert.IsFalse(GitIgnoreSemantics.IsPathIgnoredConsideringIndex(repo, "app.log"));
            }
        }

        [TestMethod]
        public void ConflictedFileMatchingGitignore_IsNotIgnored()
        {
            File.WriteAllText(Path.Combine(_testRepoPath, "app.log"), "base");

            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, "app.log");
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit("Track log file", signature, signature);
            }

            File.WriteAllText(Path.Combine(_testRepoPath, ".gitignore"), "*.log\n");

            using (var repo = new Repository(_testRepoPath))
            {
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                Commands.Stage(repo, ".gitignore");
                repo.Commit("Ignore log files", signature, signature);

                var initialBranch = repo.Head;
                var side = repo.CreateBranch("side");
                Commands.Checkout(repo, side);
                File.WriteAllText(Path.Combine(_testRepoPath, "app.log"), "side");
                Commands.Stage(repo, "app.log");
                repo.Commit("Change on side", signature, signature);

                Commands.Checkout(repo, initialBranch);
                File.WriteAllText(Path.Combine(_testRepoPath, "app.log"), "main");
                Commands.Stage(repo, "app.log");
                repo.Commit("Change on main", signature, signature);

                repo.Merge(side, signature, new MergeOptions { FileConflictStrategy = CheckoutFileConflictStrategy.Normal });

                Assert.IsNotNull(repo.Index.Conflicts["app.log"]);
                Assert.IsNull(repo.Index["app.log"]);
                Assert.IsFalse(GitIgnoreSemantics.IsPathIgnoredConsideringIndex(repo, "app.log"));
            }
        }
    }
}
