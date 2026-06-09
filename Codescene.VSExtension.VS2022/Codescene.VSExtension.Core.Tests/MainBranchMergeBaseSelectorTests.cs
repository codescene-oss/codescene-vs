// Copyright (c) CodeScene. All rights reserved.

using System.Diagnostics;
using Codescene.VSExtension.Core.Application.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class MainBranchMergeBaseSelectorTests
    {
        private string _testRepoPath;

        [TestInitialize]
        public void Setup()
        {
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-merge-base-{Guid.NewGuid()}");
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
        public void FindClosest_WithNullRepo_ReturnsNull()
        {
            var result = MainBranchMergeBaseSelector.FindClosest(null);

            Assert.IsNull(result, "Should return null for null repository");
        }

        [TestMethod]
        public void FindClosest_WithEmptyRepo_ReturnsNull()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var result = MainBranchMergeBaseSelector.FindClosest(repo);

                Assert.IsNull(result, "Should return null when repo has no HEAD tip");
            }
        }

        [TestMethod]
        public void FindClosest_WhenDefaultBranchExists_UsesDefaultBranchForMergeBase()
        {
            CommitFile("README.md", "# Test", "Initial commit");
            ExecGit("branch -m main");

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            ExecGit("checkout -b feature-branch");
            CommitFile("feature.cs", "class Feature {}", "Add feature");

            using (var repo = new Repository(_testRepoPath))
            {
                var result = MainBranchMergeBaseSelector.FindClosest(repo);

                Assert.IsNotNull(result, "Should find a merge base commit");
            }
        }

        [TestMethod]
        public void FindClosest_WhenDefaultBranchYieldsMergeBase_ReturnsEarlyWithoutCheckingAllBranches()
        {
            CommitFile("README.md", "# Test", "Initial commit");
            ExecGit("branch -m main");

            var initialCommitSha = GetHeadSha();

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            ExecGit("checkout -b feature-branch");
            CommitFile("feature.cs", "class Feature {}", "Add feature");

            using (var repo = new Repository(_testRepoPath))
            {
                var result = MainBranchMergeBaseSelector.FindClosest(repo);

                Assert.IsNotNull(result, "Should find a merge base");
                Assert.AreEqual(initialCommitSha, result.Sha, "Should return the merge base from default branch");
            }
        }

        [TestMethod]
        public void FindClosest_WhenDefaultBranchHasNoResolvableRef_DoesNotFallBackToAllBranches()
        {
            CommitFile("README.md", "# Test", "Initial commit");
            ExecGit("branch -m master");

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/HEAD", "refs/remotes/origin/missing-main");
            }

            ExecGit("checkout -b feature-branch");
            CommitFile("feature.cs", "class Feature {}", "Add feature");

            using (var repo = new Repository(_testRepoPath))
            {
                var result = MainBranchMergeBaseSelector.FindClosest(repo);

                Assert.IsNull(result, "Should not fall back to master when configured default branch is missing");
            }
        }

        [TestMethod]
        public void FindClosest_WhenDefaultBranchIsMain_DoesNotFallBackToMaster()
        {
            CommitFile("README.md", "# Master", "Initial on master");
            ExecGit("branch -m master");
            CommitFile("master-only.cs", "class M {}", "Master commit");

            ExecGit("checkout --orphan main");
            var mainFile = Path.Combine(_testRepoPath, "main.md");
            File.WriteAllText(mainFile, "# Main");
            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, "main.md");
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit("Initial on main", signature, signature);
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            ExecGit("checkout master");
            ExecGit("checkout -b feature-branch");
            CommitFile("feature.cs", "class Feature {}", "Add feature");

            using (var repo = new Repository(_testRepoPath))
            {
                var selection = MainBranchMergeBaseSelector.Select(repo);

                Assert.IsNull(selection.Commit, "Should not use master when origin/HEAD default is main");
            }
        }

        [TestMethod]
        public void Select_WhenDefaultBranchYieldsMergeBase_ReportsBaselineBranchName()
        {
            CommitFile("README.md", "# Test", "Initial commit");
            ExecGit("branch -m main");

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            ExecGit("checkout -b feature-branch");
            CommitFile("feature.cs", "class Feature {}", "Add feature");

            using (var repo = new Repository(_testRepoPath))
            {
                var selection = MainBranchMergeBaseSelector.Select(repo);

                Assert.IsNotNull(selection.Commit, "Should find a merge base");
                Assert.AreEqual("main", selection.BaselineBranchName, "Should report the default baseline branch");
            }
        }

        [TestMethod]
        public void FindClosest_WhenNoDefaultBranch_FallsBackToAllBranches()
        {
            CommitFile("README.md", "# Test", "Initial commit on main");
            ExecGit("branch -m main");

            ExecGit("checkout -b feature-branch");
            CommitFile("feature.cs", "class Feature {}", "Add feature");

            using (var repo = new Repository(_testRepoPath))
            {
                var result = MainBranchMergeBaseSelector.FindClosest(repo);

                Assert.IsNotNull(result, "Should find merge base via fallback to MainBranchNames.All");
            }
        }

        [TestMethod]
        public void FindClosest_WhenOnMainBranch_DoesNotUseSelfAsMergeBase()
        {
            CommitFile("README.md", "# Test", "Initial commit");
            ExecGit("branch -m main");

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var result = MainBranchMergeBaseSelector.FindClosest(repo);

                Assert.IsNull(result, "Should return null when on main branch with no other candidates");
            }
        }

        [TestMethod]
        public void FindClosest_WithMultipleCandidates_ReturnsClosestReachable()
        {
            CommitFile("README.md", "# Test", "Initial commit");
            ExecGit("branch -m main");

            CommitFile("file1.cs", "class A {}", "Second commit on main");

            ExecGit("checkout -b develop");
            CommitFile("develop.cs", "class Develop {}", "Commit on develop");

            ExecGit("checkout -b feature-branch");
            CommitFile("feature.cs", "class Feature {}", "Add feature");

            using (var repo = new Repository(_testRepoPath))
            {
                var result = MainBranchMergeBaseSelector.FindClosest(repo);

                Assert.IsNotNull(result, "Should find a merge base from available candidates");
            }
        }

        private void CommitFile(string filename, string content, string message)
        {
            var filePath = Path.Combine(_testRepoPath, filename);
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

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

        private string GetHeadSha()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                return repo.Head.Tip.Sha;
            }
        }
    }
}
