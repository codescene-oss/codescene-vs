// Copyright (c) CodeScene. All rights reserved.

using System.Diagnostics;
using Codescene.VSExtension.Core.Application.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class MainBranchNamesTests
    {
        private string _testRepoPath;

        [TestInitialize]
        public void Setup()
        {
            MainBranchNames.ClearDefaultBranchCache();

            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-main-branch-{Guid.NewGuid()}");
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
        public void GetDefaultBranch_WithoutOriginHead_ReturnsNull()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var result = MainBranchNames.GetDefaultBranch(repo);

                Assert.IsNull(result, "Should return null when no origin/HEAD exists");
            }
        }

        [TestMethod]
        public void GetDefaultBranch_WithOriginHead_ReturnsDefaultBranch()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var result = MainBranchNames.GetDefaultBranch(repo);

                Assert.AreEqual("main", result, "Should return 'main' when origin/HEAD points to main");
            }
        }

        [TestMethod]
        public void GetDefaultBranch_WithNullRepo_ReturnsNull()
        {
            var result = MainBranchNames.GetDefaultBranch(null);

            Assert.IsNull(result, "Should return null for null repository");
        }

        [TestMethod]
        public void IsMainBranch_WithRepo_WhenDefaultIsMain_OnlyMainIsMainBranch()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
                repo.CreateBranch("master");
            }

            using (var repo = new Repository(_testRepoPath))
            {
                Assert.IsTrue(MainBranchNames.IsMainBranch(repo, "main"), "main should be recognized as main branch");
                Assert.IsFalse(MainBranchNames.IsMainBranch(repo, "master"), "master should NOT be main when origin/HEAD points to main");
                Assert.IsFalse(MainBranchNames.IsMainBranch(repo, "develop"), "develop should NOT be main when origin/HEAD points to main");
            }
        }

        [TestMethod]
        public void IsMainBranch_WithRepo_WhenDefaultIsMaster_OnlyMasterIsMainBranch()
        {
            ExecGit("branch -m master");

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/master", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/master"]);
                repo.CreateBranch("main");
            }

            using (var repo = new Repository(_testRepoPath))
            {
                Assert.IsTrue(MainBranchNames.IsMainBranch(repo, "master"), "master should be recognized as main branch");
                Assert.IsFalse(MainBranchNames.IsMainBranch(repo, "main"), "main should NOT be main when origin/HEAD points to master");
            }
        }

        [TestMethod]
        public void IsMainBranch_WithRepo_WithoutOriginHead_FallsBackToStaticList()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                Assert.IsTrue(MainBranchNames.IsMainBranch(repo, "main"), "main should be in fallback list");
                Assert.IsTrue(MainBranchNames.IsMainBranch(repo, "master"), "master should be in fallback list");
                Assert.IsTrue(MainBranchNames.IsMainBranch(repo, "develop"), "develop should be in fallback list");
                Assert.IsTrue(MainBranchNames.IsMainBranch(repo, "trunk"), "trunk should be in fallback list");
                Assert.IsFalse(MainBranchNames.IsMainBranch(repo, "feature-branch"), "feature-branch should not be main");
            }
        }

        [TestMethod]
        public void IsMainBranch_WithRepo_IsCaseInsensitive()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            using (var repo = new Repository(_testRepoPath))
            {
                Assert.IsTrue(MainBranchNames.IsMainBranch(repo, "main"), "main should match");
                Assert.IsTrue(MainBranchNames.IsMainBranch(repo, "Main"), "Main should match (case-insensitive)");
                Assert.IsTrue(MainBranchNames.IsMainBranch(repo, "MAIN"), "MAIN should match (case-insensitive)");
            }
        }

        [TestMethod]
        public void IsMainBranch_WithRepo_WithNullBranchName_ReturnsFalse()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                Assert.IsFalse(MainBranchNames.IsMainBranch(repo, null), "Should return false for null branch name");
                Assert.IsFalse(MainBranchNames.IsMainBranch(repo, string.Empty), "Should return false for empty branch name");
            }
        }

        [TestMethod]
        public void IsMainBranch_WithoutRepo_UsesStaticListOnly()
        {
            Assert.IsTrue(MainBranchNames.IsMainBranch("main"), "main should be recognized");
            Assert.IsTrue(MainBranchNames.IsMainBranch("master"), "master should be recognized");
            Assert.IsTrue(MainBranchNames.IsMainBranch("develop"), "develop should be recognized");
            Assert.IsFalse(MainBranchNames.IsMainBranch("feature"), "feature should not be recognized");
        }

        [TestMethod]
        public void IsMainBranch_WhenOriginHeadPointsToMain_AndOnMasterBranch_MainStillRecognizedAsMainBranch()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
                repo.CreateBranch("master");
            }

            ExecGit("checkout master");

            using (var repo = new Repository(_testRepoPath))
            {
                Assert.AreEqual("main", MainBranchNames.GetDefaultBranch(repo), "Default branch should still be main");
                Assert.IsTrue(MainBranchNames.IsMainBranch(repo, "main"), "main should still be the main branch");
                Assert.IsFalse(MainBranchNames.IsMainBranch(repo, "master"), "master should NOT be main even when checked out");
            }
        }

        [TestMethod]
        public void GetDefaultBranch_WithCustomDefaultBranch_ReturnsCustomName()
        {
            ExecGit("branch -m custom-main");

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/custom-main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/custom-main"]);
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var result = MainBranchNames.GetDefaultBranch(repo);

                Assert.AreEqual("custom-main", result, "Should return custom default branch name");
                Assert.IsTrue(MainBranchNames.IsMainBranch(repo, "custom-main"), "Custom branch should be recognized as main");
                Assert.IsFalse(MainBranchNames.IsMainBranch(repo, "main"), "main should NOT be main when origin/HEAD points elsewhere");
            }
        }

        [TestMethod]
        public void GetDefaultBranch_WhenTargetDoesNotStartWithExpectedPrefix_ReturnsNull()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", "refs/heads/main", true);
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var result = MainBranchNames.GetDefaultBranch(repo);

                Assert.IsNull(result, "Should return null when target doesn't start with refs/remotes/origin/");
            }
        }

        [TestMethod]
        public void GetDefaultBranch_WithConfigBaselineBranch_ReturnsConfiguredBranch()
        {
            WriteConfig("{\"baseline_branch\":\"release\"}");

            using (var repo = new Repository(_testRepoPath))
            {
                var result = MainBranchNames.GetDefaultBranch(repo);

                Assert.AreEqual("release", result);
                Assert.IsTrue(MainBranchNames.IsMainBranch(repo, "release"));
                Assert.IsFalse(MainBranchNames.IsMainBranch(repo, "main"));
            }
        }

        [TestMethod]
        public void GetDefaultBranch_ConfigOverridesOriginHead()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            WriteConfig("{\"baseline_branch\":\"develop\"}");

            using (var repo = new Repository(_testRepoPath))
            {
                Assert.AreEqual("develop", MainBranchNames.GetDefaultBranch(repo));
                Assert.IsFalse(MainBranchNames.IsMainBranch(repo, "main"));
            }
        }

        [TestMethod]
        public void GetDefaultBranch_InvalidConfig_FallsBackToOriginHead()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            WriteConfig("not json");

            using (var repo = new Repository(_testRepoPath))
            {
                Assert.AreEqual("main", MainBranchNames.GetDefaultBranch(repo));
            }
        }

        [TestMethod]
        public void GetDefaultBranch_WhenOriginHeadTargetIsEmpty_ReturnsNull()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);

                var originHeadPath = Path.Combine(_testRepoPath, ".git", "refs", "remotes", "origin", "HEAD");
                File.WriteAllText(originHeadPath, "ref: \n");

                var result = MainBranchNames.GetDefaultBranch(repo);

                Assert.IsNull(result);
            }
        }

        [TestMethod]
        public void GetDefaultBranch_WhenExceptionOccurs_ReturnsNull()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);

                var originHeadPath = Path.Combine(_testRepoPath, ".git", "refs", "remotes", "origin", "HEAD");
                File.WriteAllText(originHeadPath, "not a valid ref\n");

                var result = MainBranchNames.GetDefaultBranch(repo);

                Assert.IsNull(result, "Should return null when exception occurs");
            }
        }

        [TestMethod]
        public void GetDefaultBranch_WhenBaselineConfigIsEmpty_UsesOriginHead()
        {
            WriteConfig("{\"baseline_branch\":\"\"}");

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var result = MainBranchNames.GetDefaultBranch(repo);

                Assert.AreEqual("main", result);
            }
        }

        [TestMethod]
        public void GetDefaultBranch_WithBareRepository_ResolvesOriginHead()
        {
            var barePath = Path.Combine(Path.GetTempPath(), $"test-bare-main-{Guid.NewGuid()}");

            try
            {
                ExecGit($"clone --bare \"{_testRepoPath}\" \"{barePath}\"");

                using (var repo = new Repository(barePath))
                {
                    repo.Refs.Add("refs/remotes/origin/main", repo.Branches["main"].Tip.Id);
                    repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
                }

                using (var repo = new Repository(barePath))
                {
                    Assert.IsTrue(string.IsNullOrEmpty(repo.Info.WorkingDirectory));

                    var result = MainBranchNames.GetDefaultBranch(repo);

                    Assert.AreEqual("main", result);
                }
            }
            finally
            {
                if (Directory.Exists(barePath))
                {
                    try
                    {
                        Directory.Delete(barePath, true);
                    }
                    catch
                    {
                    }
                }
            }
        }

        [TestMethod]
        public void GetDefaultBranch_CachesResult()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            string firstResult;
            using (var repo = new Repository(_testRepoPath))
            {
                firstResult = MainBranchNames.GetDefaultBranch(repo);
                Assert.AreEqual("main", firstResult, "First call should return main");
            }

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Remove(repo.Refs["refs/remotes/origin/HEAD"]);
                repo.Refs.Add("refs/remotes/origin/develop", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/develop"]);
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var cachedResult = MainBranchNames.GetDefaultBranch(repo);
                Assert.AreEqual("main", cachedResult, "Should return cached result even though underlying state changed");
            }
        }

        [TestMethod]
        public void GetDefaultBranch_DoesNotCacheNullResult()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var firstResult = MainBranchNames.GetDefaultBranch(repo);
                Assert.IsNull(firstResult, "First call should return null when no origin/HEAD");
            }

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var secondResult = MainBranchNames.GetDefaultBranch(repo);
                Assert.AreEqual("main", secondResult, "Should return fresh result since null was not cached");
            }
        }

        [TestMethod]
        public void ClearDefaultBranchCache_InvalidatesCache()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/main", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var firstResult = MainBranchNames.GetDefaultBranch(repo);
                Assert.AreEqual("main", firstResult);
            }

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Remove(repo.Refs["refs/remotes/origin/HEAD"]);
                repo.Refs.Add("refs/remotes/origin/develop", repo.Head.Tip.Id);
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/develop"]);
            }

            MainBranchNames.ClearDefaultBranchCache(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                var freshResult = MainBranchNames.GetDefaultBranch(repo);
                Assert.AreEqual("develop", freshResult, "Should return fresh result after cache clear");
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
