// Copyright (c) CodeScene. All rights reserved.

using System.Diagnostics;
using Codescene.VSExtension.Core.Application.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class DefaultBranchGateTests
    {
        private string _testRepoPath;

        [TestInitialize]
        public void Setup()
        {
            MainBranchNames.ClearDefaultBranchCache();
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-default-branch-gate-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testRepoPath);
            Repository.Init(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Config.Set("user.email", "test@example.com");
                repo.Config.Set("user.name", "Test User");
            }

            CommitFile("README.md", "# Test", "Initial commit");
            ExecGit("branch -m main");
        }

        [TestCleanup]
        public void Cleanup()
        {
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
        }

        [TestMethod]
        public void ShouldSkip_WhenRepoIsNull_ReturnsFalse()
        {
            var gate = new DefaultBranchGate(_testRepoPath);

            var result = gate.ShouldSkip(null);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldSkip_WhenCurrentBranchIsEmpty_ReturnsFalse()
        {
            SetupOriginHead("main");

            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Checkout(repo, repo.Head.Tip);
            }

            var gate = new DefaultBranchGate(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                var result = gate.ShouldSkip(repo);

                Assert.IsFalse(result, "Should return false in detached HEAD state");
            }
        }

        [TestMethod]
        public void ShouldSkip_WhenOnMainBranchWithOriginHead_ReturnsTrue()
        {
            SetupOriginHead("main");

            var gate = new DefaultBranchGate(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                var result = gate.ShouldSkip(repo);

                Assert.IsTrue(result, "Should skip when current branch matches default branch");
            }
        }

        [TestMethod]
        public void ShouldSkip_WhenOnFeatureBranch_ReturnsFalse()
        {
            SetupOriginHead("main");
            ExecGit("checkout -b feature");

            var gate = new DefaultBranchGate(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                var result = gate.ShouldSkip(repo);

                Assert.IsFalse(result, "Should not skip when on feature branch");
            }
        }

        [TestMethod]
        public void ShouldSkip_ComparisonIsCaseInsensitive()
        {
            SetupOriginHead("MAIN");

            var gate = new DefaultBranchGate(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                var result = gate.ShouldSkip(repo);

                Assert.IsTrue(result, "main should match MAIN case-insensitively");
            }
        }

        [TestMethod]
        public void ShouldSkip_CachesDefaultBranchAfterFirstFetch()
        {
            SetupOriginHead("main");

            var gate = new DefaultBranchGate(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                var firstResult = gate.ShouldSkip(repo);
                Assert.IsTrue(firstResult);
            }

            SetupOriginHead("develop");

            using (var repo = new Repository(_testRepoPath))
            {
                var secondResult = gate.ShouldSkip(repo);

                Assert.IsTrue(secondResult, "Should still use cached 'main' as default branch");
            }
        }

        [TestMethod]
        public void ShouldSkip_CurrentBranchIsComputedFreshEachCall()
        {
            SetupOriginHead("main");

            var gate = new DefaultBranchGate(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                var result = gate.ShouldSkip(repo);
                Assert.IsTrue(result, "Should skip when on main");
            }

            ExecGit("checkout -b feature");

            using (var repo = new Repository(_testRepoPath))
            {
                var result = gate.ShouldSkip(repo);
                Assert.IsFalse(result, "Should not skip when on feature branch");
            }

            ExecGit("checkout main");

            using (var repo = new Repository(_testRepoPath))
            {
                var result = gate.ShouldSkip(repo);
                Assert.IsTrue(result, "Should skip again when back on main");
            }
        }

        [TestMethod]
        public void ShouldSkip_WhenNoOriginHead_ReturnsFalse()
        {
            var gate = new DefaultBranchGate(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                var result = gate.ShouldSkip(repo);

                Assert.IsFalse(result, "Should not skip when default branch cannot be determined");
            }
        }

        [TestMethod]
        public void ShouldSkipForCurrentBranch_WhenOnMainBranch_ReturnsTrue()
        {
            SetupOriginHead("main");

            var gate = new DefaultBranchGate(_testRepoPath);

            var result = gate.ShouldSkipForCurrentBranch();

            Assert.IsTrue(result, "Should skip when current branch matches default branch");
        }

        [TestMethod]
        public void ShouldSkipForCurrentBranch_WhenOnFeatureBranch_ReturnsFalse()
        {
            SetupOriginHead("main");
            ExecGit("checkout -b feature");

            var gate = new DefaultBranchGate(_testRepoPath);

            var result = gate.ShouldSkipForCurrentBranch();

            Assert.IsFalse(result, "Should not skip when on feature branch");
        }

        [TestMethod]
        public void ShouldSkip_UsesBaselineBranchFromConfig()
        {
            ExecGit("checkout -b develop");
            WriteConfig("{\"baseline_branch\":\"develop\"}");

            var gate = new DefaultBranchGate(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                var result = gate.ShouldSkip(repo);

                Assert.IsTrue(result, "Should skip when current branch matches configured baseline branch");
            }
        }

        [TestMethod]
        public void ShouldSkip_ConfigOverridesOriginHead()
        {
            SetupOriginHead("main");
            ExecGit("checkout -b develop");
            WriteConfig("{\"baseline_branch\":\"develop\"}");

            var gate = new DefaultBranchGate(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                var result = gate.ShouldSkip(repo);

                Assert.IsTrue(result, "Should use configured baseline branch instead of origin/HEAD");
            }
        }

        private void SetupOriginHead(string branchName)
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var existingOriginHead = repo.Refs[$"refs/remotes/origin/HEAD"];
                if (existingOriginHead != null)
                {
                    repo.Refs.Remove(existingOriginHead);
                }

                var existingRef = repo.Refs[$"refs/remotes/origin/{branchName}"];
                if (existingRef != null)
                {
                    repo.Refs.Remove(existingRef);
                }

                repo.Refs.Add($"refs/remotes/origin/{branchName}", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs[$"refs/remotes/origin/{branchName}"]);
            }
        }

        private void WriteConfig(string json)
        {
            var codesceneDir = Path.Combine(_testRepoPath, CodesceneFileWatcher.CodesceneDir);
            Directory.CreateDirectory(codesceneDir);
            File.WriteAllText(Path.Combine(codesceneDir, CodesceneFileWatcher.ConfigFileName), json);
        }

        private void CommitFile(string filename, string content, string message)
        {
            var filePath = Path.Combine(_testRepoPath, filename);
            File.WriteAllText(filePath, content);

            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, filename);
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit(message, signature, signature);
            }
        }

        private void ExecGit(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = _testRepoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using (var process = Process.Start(psi))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    var error = process.StandardError.ReadToEnd();
                    throw new Exception($"Git command failed: {args}\n{error}");
                }
            }
        }
    }
}
