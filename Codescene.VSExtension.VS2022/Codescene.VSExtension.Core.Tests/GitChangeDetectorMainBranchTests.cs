// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeDetectorMainBranchTests : GitChangeDetectorTestBase
    {
        [TestMethod]
        public void Constructor_InitializesWithLogger()
        {
            var detector = new GitChangeDetector(_fakeLogger, _fakeSupportedFileChecker);

            Assert.IsNotNull(detector);
        }

        [TestMethod]
        public void GetMainBranchCandidates_DetectsMainBranch()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var currentBranch = repo.Head.FriendlyName;
                var candidates = GetMainBranchCandidates(repo);

                Assert.IsNotEmpty(candidates, "Should detect at least one main branch candidate");
                Assert.IsTrue(
                    candidates.Contains("main") || candidates.Contains("master"),
                    "Should detect either 'main' or 'master' as a candidate");
                Assert.Contains(
                    currentBranch,
                    candidates,
                    $"Should detect current branch '{currentBranch}' as a candidate");
            }
        }

        [TestMethod]
        public void GetMainBranchCandidates_DetectsDevelopBranch()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var developBranch = repo.CreateBranch("develop");
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var candidates = GetMainBranchCandidates(repo);

                Assert.Contains(
                    "develop",
                    candidates,
                    "Should detect 'develop' branch when it exists");
            }
        }

        [TestMethod]
        public void GetMainBranchCandidates_DetectsAllSupportedBranches()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                repo.CreateBranch("develop");
                repo.CreateBranch("trunk");
                repo.CreateBranch("dev");
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var candidates = GetMainBranchCandidates(repo);

                var expectedBranches = new[] { "main", "master", "develop", "trunk", "dev" };
                var currentBranch = repo.Head.FriendlyName;

                Assert.Contains("develop", candidates, "Should detect 'develop'");
                Assert.Contains("trunk", candidates, "Should detect 'trunk'");
                Assert.Contains("dev", candidates, "Should detect 'dev'");

                foreach (var branch in expectedBranches)
                {
                    if (repo.Branches[branch] != null)
                    {
                        Assert.Contains(
                            branch,
                            candidates,
                            $"Should detect existing branch '{branch}'");
                    }
                }
            }
        }

        [TestMethod]
        public void GetMainBranchCandidates_ExcludesRemoteBranches()
        {
            ExecGit("remote add origin https://github.com/test/repo.git");
            ExecGit("config remote.origin.fetch +refs/heads/*:refs/remotes/origin/*");

            using (var repo = new Repository(_testRepoPath))
            {
                var currentBranch = repo.Head.FriendlyName;
                repo.Refs.Add($"refs/remotes/origin/{currentBranch}", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/develop", repo.Head.Tip.Id);
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var candidates = GetMainBranchCandidates(repo);

                var remoteBranches = repo.Branches.Where(b => b.IsRemote).Select(b => b.FriendlyName).ToList();
                Assert.IsNotEmpty(remoteBranches, "Should have created remote branches");

                foreach (var remoteBranch in remoteBranches)
                {
                    Assert.DoesNotContain(
                        remoteBranch,
                        candidates,
                        $"Should not include remote branch '{remoteBranch}'");
                }
            }
        }

        [TestMethod]
        public void GetMainBranchCandidates_ReturnsEmpty_WhenNoMainBranches()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var currentBranch = repo.Head.FriendlyName;
                var featureBranch = repo.CreateBranch("feature-xyz");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            ExecGit($"branch -D master");

            try
            {
                ExecGit($"branch -D main");
            }
            catch
            {
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var candidates = GetMainBranchCandidates(repo);

                Assert.IsEmpty(
                    candidates,
                    "Should return empty list when no main branch candidates exist");
            }
        }

        [TestMethod]
        public void GetMainBranchCandidates_CachesCandidates()
        {
            List<string> firstCall;
            List<string> secondCall;

            using (var repo = new Repository(_testRepoPath))
            {
                firstCall = GetMainBranchCandidates(repo);
            }

            using (var repo = new Repository(_testRepoPath))
            {
                secondCall = GetMainBranchCandidates(repo);
            }

            CollectionAssert.AreEqual(
                firstCall,
                secondCall,
                "Subsequent calls should return cached candidates");
        }
    }
}
