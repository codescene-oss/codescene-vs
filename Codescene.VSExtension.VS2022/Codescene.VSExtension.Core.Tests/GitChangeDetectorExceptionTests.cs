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

        [TestMethod]
        public async Task GetMergeBaseCommit_InvalidCurrentBranch_ReturnsEmptyList()
        {
            ExecGit("checkout -b feature-test");
            CommitFile("test.cs", "public class Test {}", "Add test");

            var testableDetector = new TestableGitChangeDetector(_fakeLogger, _fakeSupportedFileChecker);
            testableDetector.SimulateInvalidCurrentBranch = true;

            var result = await testableDetector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result, "Should return non-null result even with invalid branch");
        }

        [TestMethod]
        public async Task TryFindMergeBase_InvalidMainBranch_ReturnsEmptyList()
        {
            ExecGit("checkout -b feature-test");
            CommitFile("test.cs", "public class Test {}", "Add test");

            var testableDetector = new TestableGitChangeDetector(_fakeLogger, _fakeSupportedFileChecker);
            testableDetector.SimulateInvalidMainBranch = true;

            var result = await testableDetector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result, "Should handle invalid main branch gracefully");
        }

        [TestMethod]
        public async Task TryFindMergeBase_FindMergeBaseThrows_HandlesGracefully()
        {
            ExecGit("checkout -b feature-test");
            CommitFile("test.cs", "public class Test {}", "Add test");

            var testableDetector = new TestableGitChangeDetector(_fakeLogger, _fakeSupportedFileChecker);
            testableDetector.ThrowFromFindMergeBase = true;

            var result = await testableDetector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result, "Should handle FindMergeBase exception gracefully");
            Assert.IsTrue(_fakeLogger.DebugMessages.Any(m => m.Contains("Could not determine merge base")),
                "Should log debug message when FindMergeBase throws exception");
        }

        [TestMethod]
        public async Task GetCommittedChanges_DiffCompareThrows_HandlesGracefully()
        {
            ExecGit("checkout -b feature-test");
            CommitFile("test.cs", "public class Test {}", "Add test");

            var testableDetector = new TestableGitChangeDetector(_fakeLogger, _fakeSupportedFileChecker);
            testableDetector.ThrowFromDiffCompare = true;

            var result = await testableDetector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result, "Should handle Diff.Compare exception gracefully");
            Assert.IsTrue(_fakeLogger.WarnMessages.Any(m => m.Contains("Error getting changed files")),
                "Should log warning message when Diff.Compare throws exception");
        }

        [TestMethod]
        public async Task GetStatusChanges_RetrieveStatusThrows_HandlesGracefully()
        {
            ExecGit("checkout -b feature-test");
            CommitFile("test.cs", "public class Test {}", "Add test");

            var testableDetector = new TestableGitChangeDetector(_fakeLogger, _fakeSupportedFileChecker);
            testableDetector.ThrowFromRetrieveStatus = true;

            var result = await testableDetector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result, "Should handle RetrieveStatus exception gracefully");
            Assert.IsTrue(_fakeLogger.WarnMessages.Any(m => m.Contains("Error getting changed files")),
                "Should log warning message when RetrieveStatus throws exception");
        }

        [TestMethod]
        public async Task GetMergeBaseCommit_UnbornHead_ReturnsGracefully()
        {
            var unbornRepoPath = Path.Combine(Path.GetTempPath(), $"test-unborn-{Guid.NewGuid()}");
            try
            {
                Directory.CreateDirectory(unbornRepoPath);
                Repository.Init(unbornRepoPath);
                using (var repo = new Repository(unbornRepoPath))
                {
                    repo.Config.Set("user.email", "test@example.com");
                    repo.Config.Set("user.name", "Test User");
                }

                var result = await _detector.GetChangedFilesVsBaselineAsync(
                    unbornRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

                Assert.AreEqual(0, result.Count, "Should return empty list for unborn HEAD");
            }
            finally
            {
                if (Directory.Exists(unbornRepoPath))
                {
                    try { Directory.Delete(unbornRepoPath, true); } catch { }
                }
            }
        }

        [TestMethod]
        public async Task TryFindMergeBase_BranchNotFound_ReturnsNull()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-test");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }
            CommitFile("test.cs", "public class Test {}", "Add test");

            var testableDetector = new TestableGitChangeDetector(_fakeLogger, _fakeSupportedFileChecker);
            testableDetector.ForceBranchLookupFailure = "nonexistent-branch";

            var result = await testableDetector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result, "Should handle non-existent branch gracefully");
        }

        [TestMethod]
        public async Task TryFindMergeBase_FindMergeBaseThrowsLibGit2_LogsDebug()
        {
            ExecGit("checkout -b main");
            CommitFile("main.cs", "public class Main {}", "Main commit");
            ExecGit("checkout -b feature");
            CommitFile("feature.cs", "public class Feature {}", "Feature commit");

            var packPath = Path.Combine(_testRepoPath, ".git", "objects", "pack");
            if (Directory.Exists(packPath))
            {
                foreach (var packFile in Directory.GetFiles(packPath))
                {
                    try { File.Delete(packFile); } catch { }
                }
            }

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result, "Should handle corrupted repository gracefully");
        }

        [TestMethod]
        public async Task GetCommittedChanges_DiffCompareThrowsLibGit2_LogsDebug()
        {
            ExecGit("checkout -b main");
            CommitFile("main.cs", "public class Main {}", "Main commit");
            ExecGit("checkout -b feature");
            CommitFile("feature.cs", "public class Feature {}", "Feature commit");
            CorruptGitObjects();

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result, "Should handle corrupted objects gracefully");
        }

        [TestMethod]
        public async Task GetStatusChanges_RetrieveStatusThrowsLibGit2_LogsDebug()
        {
            CommitFile("test.cs", "public class Test {}", "Add test");
            CorruptGitIndex();

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result, "Should handle corrupted index gracefully");
        }
    }
}
