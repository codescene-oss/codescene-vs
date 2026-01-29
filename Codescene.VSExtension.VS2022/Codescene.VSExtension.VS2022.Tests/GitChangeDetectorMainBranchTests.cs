using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.VS2022.Tests
{
    [TestClass]
    public class GitChangeDetectorMainBranchTests : GitChangeDetectorTestBase
    {
        [TestMethod]
        public void GetMainBranchCandidates_DetectsMainBranch()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var currentBranch = repo.Head.FriendlyName;
                var candidates = GetMainBranchCandidates(repo);

                Assert.IsTrue(candidates.Count > 0, "Should detect at least one main branch candidate");
                Assert.IsTrue(candidates.Contains("main") || candidates.Contains("master"),
                    "Should detect either 'main' or 'master' as a candidate");
                Assert.IsTrue(candidates.Contains(currentBranch),
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

                Assert.IsTrue(candidates.Contains("develop"),
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

                Assert.IsTrue(candidates.Contains("develop"), "Should detect 'develop'");
                Assert.IsTrue(candidates.Contains("trunk"), "Should detect 'trunk'");
                Assert.IsTrue(candidates.Contains("dev"), "Should detect 'dev'");

                foreach (var branch in expectedBranches)
                {
                    if (repo.Branches[branch] != null)
                    {
                        Assert.IsTrue(candidates.Contains(branch),
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
                Assert.IsTrue(remoteBranches.Count > 0, "Should have created remote branches");

                foreach (var remoteBranch in remoteBranches)
                {
                    Assert.IsFalse(candidates.Contains(remoteBranch),
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

                Assert.AreEqual(0, candidates.Count,
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

            CollectionAssert.AreEqual(firstCall, secondCall,
                "Subsequent calls should return cached candidates");
        }
    }
}
