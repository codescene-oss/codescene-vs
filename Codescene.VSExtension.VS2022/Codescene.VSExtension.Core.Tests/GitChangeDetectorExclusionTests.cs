// Copyright (c) CodeScene. All rights reserved.

using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeDetectorExclusionTests : GitChangeDetectorTestBase
    {
        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_ExcludesSavedFiles()
        {
            var testFilePath = CommitFile("test.cs", "public class Test {}", "Add test file");

            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-test");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            CommitFile("feature.cs", "public class Feature {}", "Add feature");

            File.WriteAllText(testFilePath, "public class Test { /* modified */ }");
            _fakeSavedFilesTracker.AddSavedFile(testFilePath);

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, null, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            var normalizedResult = result.Select(f => f.Replace('/', '\\')).ToList();
            Assert.IsFalse(
                normalizedResult.Any(f => f.EndsWith("test.cs")),
                "Should exclude saved file from results");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_ActiveDocumentRemainsInChangedList()
        {
            var testFilePath = CommitFile("test.cs", "public class Test {}", "Add test file");

            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-test");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            CommitFile("feature.cs", "public class Feature {}", "Add feature");

            File.WriteAllText(testFilePath, "public class Test { /* modified */ }");
            _fakeOpenFilesObserver.SetActiveDocument(testFilePath);

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, null, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            var normalizedResult = result.Select(f => f.Replace('/', '\\')).ToList();
            Assert.IsTrue(
                normalizedResult.Any(f => f.EndsWith("test.cs")),
                "Active document must remain in changed list so delta cache is not cleared");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_ActiveDocumentWithCommittedChangesRemainsInList()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-test");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            var committedFilePath = CommitFile("committed-on-feature.cs", "public class Committed {}", "Add on feature");
            _fakeOpenFilesObserver.SetActiveDocument(committedFilePath);

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, null, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            var normalizedResult = result.Select(f => f.Replace('/', '\\')).ToList();
            Assert.IsTrue(
                normalizedResult.Any(f => f.EndsWith("committed-on-feature.cs")),
                "Active document must remain in changed list so delta cache is not cleared");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_FileInExclusionSet_ExcludedFromStatusChanges()
        {
            var excludedPath = Path.Combine(_testRepoPath, "excluded.cs");
            File.WriteAllText(excludedPath, "public class Excluded {}");
            _fakeSavedFilesTracker.AddSavedFile(excludedPath);

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, null, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            var normalizedResult = result.Select(f => f.Replace('/', '\\')).ToList();
            Assert.IsFalse(
                normalizedResult.Any(f => f.EndsWith("excluded.cs")),
                "File in exclusion set should be excluded from status changes");
        }
    }
}
