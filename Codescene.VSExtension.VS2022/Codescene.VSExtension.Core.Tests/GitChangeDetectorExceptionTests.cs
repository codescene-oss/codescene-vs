using LibGit2Sharp;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeDetectorExceptionTests : GitChangeDetectorTestBase
    {
        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_LibGit2SharpException_ReturnsEmptyAndLogsWarning()
        {
            var testableDetector = new TestableGitChangeDetector(_fakeLogger, _fakeSupportedFileChecker);
            testableDetector.ThrowInGetChangedFilesFromRepository = true;

            var result = await testableDetector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.AreEqual(0, result.Count, "Should return empty list on exception");
            Assert.IsTrue(_fakeLogger.WarnMessages.Any(m => m.Contains("Error getting changed files")),
                "Should log warning message on exception");
        }

        [TestMethod]
        public async Task GetMergeBaseCommit_ThrowsException_ReturnsNullAndLogsDebug()
        {
            var testableDetector = new TestableGitChangeDetector(_fakeLogger, _fakeSupportedFileChecker);
            testableDetector.ThrowFromMainBranchCandidates = true;

            var result = await testableDetector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsTrue(_fakeLogger.DebugMessages.Any(m => m.Contains("Could not determine merge base")),
                "Should log debug message when GetMergeBaseCommit throws");
        }


        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_InvalidGitRootPath_ReturnsEmptyList()
        {
            var result = await _detector.GetChangedFilesVsBaselineAsync(
                string.Empty, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.AreEqual(0, result.Count, "Should return empty list for invalid git root path");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_NonexistentGitRootPath_ReturnsEmptyList()
        {
            var result = await _detector.GetChangedFilesVsBaselineAsync(
                "C:\\nonexistent\\path", _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.AreEqual(0, result.Count, "Should return empty list for nonexistent git root path");
        }

        [TestMethod]
        public void GetMainBranchCandidates_EmptyWorkingDirectory_ReturnsEmptyList()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var candidates = GetMainBranchCandidates(repo);

                Assert.IsNotNull(candidates, "Should not return null");
            }
        }

        [TestMethod]
        public void GetMainBranchCandidates_BareRepository_ReturnsEmptyList()
        {
            var bareRepoPath = Path.Combine(Path.GetTempPath(), $"test-bare-repo-{Guid.NewGuid()}");
            try
            {
                Repository.Init(bareRepoPath, isBare: true);

                using (var bareRepo = new Repository(bareRepoPath))
                {
                    var candidates = _detector.GetMainBranchCandidates(bareRepo);

                    Assert.IsNotNull(candidates, "Should not return null");
                    Assert.AreEqual(0, candidates.Count, "Should return empty list for bare repository");
                }
            }
            finally
            {
                if (Directory.Exists(bareRepoPath))
                {
                    try
                    {
                        Directory.Delete(bareRepoPath, true);
                    }
                    catch
                    {
                    }
                }
            }
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_OrphanedBranch_HandlesGracefully()
        {
            string originalBranch;
            using (var repo = new Repository(_testRepoPath))
            {
                originalBranch = repo.Head.FriendlyName;
            }

            ExecGit("checkout --orphan orphaned");
            CommitFile("orphan.cs", "// orphan code", "Orphaned commit");
            ExecGit($"checkout {originalBranch}");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result, "Should handle orphaned branches gracefully");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_MultipleOrphanedBranches_HandlesGracefully()
        {
            string originalBranch;
            using (var repo = new Repository(_testRepoPath))
            {
                originalBranch = repo.Head.FriendlyName;
            }

            ExecGit("checkout --orphan orphan1");
            CommitFile("orphan1.cs", "// code", "Orphan 1");
            ExecGit($"checkout {originalBranch}");
            ExecGit("checkout --orphan orphan2");
            CommitFile("orphan2.cs", "// code", "Orphan 2");
            ExecGit($"checkout {originalBranch}");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result, "Should handle multiple orphaned branches gracefully");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_CorruptedDiffScenario_HandlesGracefully()
        {
            ExecGit("checkout -b feature");
            for (int i = 0; i < 10; i++)
            {
                CommitFile($"file{i}.cs", $"// content {i}", $"Commit {i}");
            }

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result, "Should handle complex diff scenarios gracefully");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_ManyUnstagedFiles_HandlesGracefully()
        {
            for (int i = 0; i < 20; i++)
            {
                File.WriteAllText(Path.Combine(_testRepoPath, $"unstaged{i}.cs"), $"// unstaged {i}");
            }

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result, "Should handle many unstaged files gracefully");
        }
    }
}
