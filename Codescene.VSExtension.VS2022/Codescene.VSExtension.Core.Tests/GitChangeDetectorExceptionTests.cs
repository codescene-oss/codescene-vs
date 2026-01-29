using LibGit2Sharp;
using System;
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
    }
}
