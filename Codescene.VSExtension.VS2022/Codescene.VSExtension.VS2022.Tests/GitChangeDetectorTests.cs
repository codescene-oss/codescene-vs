using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.VS2022.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Tests
{
    [TestClass]
    public class GitChangeDetectorTests
    {
        private string _testRepoPath;
        private GitChangeDetector _detector;
        private FakeLogger _fakeLogger;
        private FakeSupportedFileChecker _fakeSupportedFileChecker;
        private FakeSavedFilesTracker _fakeSavedFilesTracker;
        private FakeOpenFilesObserver _fakeOpenFilesObserver;

        [TestInitialize]
        public void Setup()
        {
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-git-detector-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testRepoPath);
            Repository.Init(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Config.Set("user.email", "test@example.com");
                repo.Config.Set("user.name", "Test User");
            }

            CommitFile("README.md", "# Test", "Initial commit");

            _fakeLogger = new FakeLogger();
            _fakeSupportedFileChecker = new FakeSupportedFileChecker();
            _fakeSavedFilesTracker = new FakeSavedFilesTracker();
            _fakeOpenFilesObserver = new FakeOpenFilesObserver();

            _detector = new GitChangeDetector(_fakeLogger, _fakeSupportedFileChecker);
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

        private List<string> GetMainBranchCandidates(Repository repo)
        {
            return _detector.GetMainBranchCandidates(repo);
        }

        private string CommitFile(string filename, string content, string message)
        {
            var filePath = Path.Combine(_testRepoPath, filename);
            File.WriteAllText(filePath, content);

            using (var repo = new Repository(_testRepoPath))
            {
                LibGit2Sharp.Commands.Stage(repo, filename);
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit(message, signature, signature);
            }

            return filePath;
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
                CreateNoWindow = true
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

        [TestMethod]
        public async Task GetChangedFiles_FindsMergeBaseWithMainBranch()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-test");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            CommitFile("feature.cs", "public class Feature {}", "Add feature");

            var changedFiles = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsTrue(changedFiles.Any(f => f.EndsWith("feature.cs")),
                "Should detect feature.cs as changed vs main branch");
        }

        [TestMethod]
        public async Task GetChangedFiles_IteratesToSecondCandidate()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var developBranch = repo.CreateBranch("develop");
                LibGit2Sharp.Commands.Checkout(repo, developBranch);
            }

            try
            {
                ExecGit($"branch -D master");
            }
            catch
            {
            }

            try
            {
                ExecGit($"branch -D main");
            }
            catch
            {
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-test");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            CommitFile("feature.cs", "public class Feature {}", "Add feature");

            var changedFiles = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsTrue(changedFiles.Any(f => f.EndsWith("feature.cs")),
                "Should find merge base with 'develop' branch when main/master not available");
        }

        [TestMethod]
        public async Task GetChangedFiles_HandlesDetachedHead()
        {
            string commitSha;
            using (var repo = new Repository(_testRepoPath))
            {
                commitSha = repo.Head.Tip.Sha;
            }

            ExecGit($"checkout {commitSha}");

            var filePath = Path.Combine(_testRepoPath, "detached.cs");
            File.WriteAllText(filePath, "public class Detached {}");

            var changedFiles = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(changedFiles,
                "Should return results even in detached HEAD state");
            Assert.IsTrue(changedFiles.Any(f => f.EndsWith("detached.cs")),
                "Should detect new file in detached HEAD state");
        }
    }
}
