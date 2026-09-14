// Copyright (c) CodeScene. All rights reserved.

using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeDetectorMergeCommitTests : GitChangeDetectorTestBase
    {
        private static readonly string[] UpstreamOnlyFiles = { "upstream-a.cs", "upstream-b.cs", "upstream-c.cs" };

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_ExcludesFilesIntroducedByMergingBaseline()
        {
            var baseCommit = PrepareRepositoryWithMergedBaseline();

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, null, _fakeSavedFilesTracker, _fakeOpenFilesObserver, baseCommit);

            Assert.IsTrue(
                result.Any(f => f.EndsWith("feature.cs")),
                $"Should include feature.cs. Found: {string.Join(", ", result)}");

            foreach (var upstreamFile in UpstreamOnlyFiles)
            {
                Assert.IsFalse(
                    result.Any(f => f.EndsWith(upstreamFile)),
                    $"Should exclude merge-introduced file {upstreamFile}. Found: {string.Join(", ", result)}");
            }
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_IncludesFeatureCommitsMadeAfterMerge()
        {
            var baseCommit = PrepareRepositoryWithMergedBaseline();
            CommitFile("after-merge.cs", "public class AfterMerge {}", "Feature work after merge");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, null, _fakeSavedFilesTracker, _fakeOpenFilesObserver, baseCommit);

            Assert.IsTrue(
                result.Any(f => f.EndsWith("feature.cs")),
                $"Should include feature.cs. Found: {string.Join(", ", result)}");
            Assert.IsTrue(
                result.Any(f => f.EndsWith("after-merge.cs")),
                $"Should include after-merge.cs. Found: {string.Join(", ", result)}");
            Assert.IsFalse(
                result.Any(f => f.EndsWith("upstream-a.cs")),
                $"Should exclude merge-introduced upstream-a.cs. Found: {string.Join(", ", result)}");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_IncludesFilesModifiedBothOnFeatureAndBaseline()
        {
            ExecGit("branch -M main");
            CommitFile("shared.cs", "public class Shared {}", "Add shared file");

            string baseCommit;
            using (var repo = new Repository(_testRepoPath))
            {
                baseCommit = repo.Head.Tip.Sha;
            }

            ExecGit("checkout -b upstream");
            CommitFile("shared.cs", "public class Shared { /* upstream */ }", "Upstream edit");

            ExecGit("checkout main");
            ExecGit("checkout -b feature");
            CommitFile("feature.cs", "public class Feature {}", "Add feature");
            CommitFile("shared.cs", "public class Shared { /* feature */ }", "Feature edit");
            ExecGit("merge upstream -m \"Merge upstream into feature\" --no-edit -X ours");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, null, _fakeSavedFilesTracker, _fakeOpenFilesObserver, baseCommit);

            Assert.IsTrue(
                result.Any(f => f.EndsWith("shared.cs")),
                $"Should include shared.cs changed on the feature branch. Found: {string.Join(", ", result)}");
        }

        private string PrepareRepositoryWithMergedBaseline()
        {
            ExecGit("branch -M main");

            string baseCommit;
            using (var repo = new Repository(_testRepoPath))
            {
                baseCommit = repo.Head.Tip.Sha;
            }

            ExecGit("checkout -b feature");
            CommitFile("feature.cs", "public class Feature {}", "Add feature file");

            ExecGit("checkout -b upstream main");
            foreach (var upstreamFile in UpstreamOnlyFiles)
            {
                CommitFile(upstreamFile, $"public class {Path.GetFileNameWithoutExtension(upstreamFile).Replace("-", "_")} {{}}", $"Add {upstreamFile}");
            }

            ExecGit("checkout feature");
            ExecGit("merge upstream -m \"Merge upstream into feature\"");

            return baseCommit;
        }
    }
}
