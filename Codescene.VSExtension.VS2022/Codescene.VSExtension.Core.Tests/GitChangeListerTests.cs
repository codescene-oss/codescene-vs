// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeListerTests : GitChangeDetectorTestBase
    {
        private GitChangeLister _lister;

        [TestInitialize]
        public void SetupLister()
        {
            _lister = new GitChangeLister(_fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger, _fakeGitService);
        }

        [TestCleanup]
        public void CleanupLister()
        {
            _lister?.Dispose();
        }

        [TestMethod]
        public void SetWorkspacePaths_UpdatesPaths()
        {
            _lister.SetWorkspacePaths(new[] { _testRepoPath });
            _lister.SetWorkspacePaths(null);
            _lister.SetWorkspacePaths(Array.Empty<string>());
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_CleanRepository_ReturnsEmptySet()
        {
            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.IsEmpty(result, "Clean repository should return no changed files");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_DetectsNewUntrackedFiles()
        {
            var newFile = Path.Combine(_testRepoPath, "new-file.cs");
            File.WriteAllText(newFile, "public class NewClass { }");

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.HasCount(1, result, "Should detect one new untracked file");
            Assert.Contains(newFile, result, "Should contain the new file");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_DetectsModifiedFiles()
        {
            var existingFile = Path.Combine(_testRepoPath, "existing.cs");
            CommitFile("existing.cs", "original content", "Add existing file");

            File.WriteAllText(existingFile, "modified content");

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.HasCount(1, result, "Should detect one modified file");
            Assert.Contains(existingFile, result, "Should contain the modified file");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_DetectsStagedFiles()
        {
            var stagedFile = Path.Combine(_testRepoPath, "staged.cs");
            File.WriteAllText(stagedFile, "staged content");

            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, "staged.cs");
            }

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.HasCount(1, result, "Should detect one staged file");
            Assert.Contains(stagedFile, result, "Should contain the staged file");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_FiltersUnsupportedFileTypes()
        {
            var supportedFile = Path.Combine(_testRepoPath, "supported.cs");
            var unsupportedFile = Path.Combine(_testRepoPath, "unsupported.txt");

            File.WriteAllText(supportedFile, "code");
            File.WriteAllText(unsupportedFile, "text");

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.HasCount(1, result, "Should only detect supported file types");
            Assert.Contains(supportedFile, result, "Should contain the .cs file");
            Assert.DoesNotContain(unsupportedFile, result, "Should not contain the .txt file");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_DetectsRenamedFiles()
        {
            CommitFile("original.cs", "content", "Add original file");

            ExecGit("mv original.cs renamed.cs");

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.IsNotEmpty(result, "Should detect renamed files");
            Assert.IsTrue(result.Any(f => f.Contains("renamed.cs")), "Should detect the new name");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_CombinesStatusAndDiffChanges()
        {
            ExecGit("checkout -b feature-branch");

            var committedFile = Path.Combine(_testRepoPath, "committed.cs");
            CommitFile("committed.cs", "committed content", "Add committed file");

            var modifiedFile = Path.Combine(_testRepoPath, "modified.cs");
            File.WriteAllText(modifiedFile, "modified content");

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.IsGreaterThanOrEqualTo(result.Count, 2, "Should detect both committed and modified files");
            Assert.Contains(committedFile, result, "Should contain the committed file");
            Assert.Contains(modifiedFile, result, "Should contain the modified file");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_HandlesFilesWithWhitespaceInNames()
        {
            var fileWithSpaces = Path.Combine(_testRepoPath, "file with spaces.cs");
            File.WriteAllText(fileWithSpaces, "content");

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.HasCount(1, result, "Should handle files with whitespace");
            Assert.Contains(fileWithSpaces, result, "Should contain the file with spaces");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_SkipsDirectoryWithTooManyUntrackedFiles()
        {
            var subdir = Path.Combine(_testRepoPath, "many-files");
            Directory.CreateDirectory(subdir);

            for (int i = 0; i < 10; i++)
            {
                File.WriteAllText(Path.Combine(subdir, $"file{i}.cs"), $"content {i}");
            }

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.IsEmpty(result, "Should skip directory with more than 5 untracked files");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_IncludesUntrackedFilesUpToLimit()
        {
            var subdir = Path.Combine(_testRepoPath, "few-files");
            Directory.CreateDirectory(subdir);

            for (int i = 0; i < 3; i++)
            {
                File.WriteAllText(Path.Combine(subdir, $"file{i}.cs"), $"content {i}");
            }

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.HasCount(3, result, "Should include all files when under the limit");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_IncludesSavedFilesEvenWhenHeuristicExceeded()
        {
            var subdir = Path.Combine(_testRepoPath, "too-many-files");
            Directory.CreateDirectory(subdir);

            var savedFile = Path.Combine(subdir, "saved-file.cs");
            File.WriteAllText(savedFile, "saved content");
            _fakeSavedFilesTracker.AddSavedFile(savedFile);

            for (int i = 0; i < 10; i++)
            {
                File.WriteAllText(Path.Combine(subdir, $"file{i}.cs"), $"content {i}");
            }

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.HasCount(1, result, "Should include saved file even when heuristic exceeded");
            Assert.Contains(savedFile, result, "Should contain the saved file");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_ReturnsAbsolutePaths()
        {
            var newFile = Path.Combine(_testRepoPath, "test.cs");
            File.WriteAllText(newFile, "content");

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.HasCount(1, result, "Should detect the file");
            var filePath = result.First();
            Assert.IsTrue(Path.IsPathRooted(filePath), "Should return absolute path");
            Assert.AreEqual(newFile, filePath, "Absolute path should match expected");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_ExcludesNonExistentFiles()
        {
            var tempFile = Path.Combine(_testRepoPath, "deleted.cs");
            File.WriteAllText(tempFile, "content");

            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, "deleted.cs");
            }

            File.Delete(tempFile);

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.DoesNotContain(tempFile, result, "Should exclude non-existent files");
        }

        [TestMethod]
        public async Task GetAllChangedFilesAsync_ExcludesDeletedFiles()
        {
            CommitFile("to-delete.cs", "content", "Add file");

            var filePath = Path.Combine(_testRepoPath, "to-delete.cs");
            File.Delete(filePath);

            var result = await _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath);

            Assert.DoesNotContain(filePath, result, "Should exclude deleted files");
        }

        [TestMethod]
        public void StartPeriodicScanning_WhenAlreadyStarted_LogsWarningAndReturns()
        {
            _lister.Initialize(_testRepoPath, new[] { _testRepoPath });
            _lister.StartPeriodicScanning(CancellationToken.None);

            _fakeLogger.ClearWarnMessages();
            _lister.StartPeriodicScanning(CancellationToken.None);

            var warnings = _fakeLogger.SnapshotWarnMessages();
            Assert.HasCount(1, warnings, "Should log warning when already started");
            Assert.IsTrue(warnings.Any(m => m.Contains("already started")), "Warning should mention already started");
        }

        [TestMethod]
        public void StopPeriodicScanning_WhenNotStarted_DoesNotThrow()
        {
            _lister.StopPeriodicScanning();
        }

        [TestMethod]
        public async Task GetChangedFilesVsMergeBaseAsync_OnFeatureBranch_ReturnsCommittedChanges()
        {
            ExecGit("checkout -b feature-branch");

            var committedFile = Path.Combine(_testRepoPath, "feature-file.cs");
            CommitFile("feature-file.cs", "feature content", "Add feature file");

            var result = await _lister.GetChangedFilesVsMergeBaseAsync(_testRepoPath, _testRepoPath);

            Assert.IsTrue(result.Any(f => f.Contains("feature-file.cs")), "Should detect committed changes on feature branch");
        }

        [TestMethod]
        public async Task GetChangedFilesVsMergeBaseAsync_OnMainBranch_ReturnsEmptySet()
        {
            var result = await _lister.GetChangedFilesVsMergeBaseAsync(_testRepoPath, _testRepoPath);

            Assert.IsEmpty(result, "Should return empty set on main branch");
        }

        [TestMethod]
        public async Task GetChangedFilesVsMergeBaseAsync_RepeatedNoMergeBaseState_LogsWarnOnce()
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

            _fakeLogger.ClearWarnMessages();

            await _lister.GetChangedFilesVsMergeBaseAsync(_testRepoPath, _testRepoPath);
            await _lister.GetChangedFilesVsMergeBaseAsync(_testRepoPath, _testRepoPath);

            var warnCount = _fakeLogger.SnapshotWarnMessages()
                .Count(m => m.Contains("can't determine merge-base"));

            Assert.AreEqual(1, warnCount, "Should warn about missing merge base only once until repo state changes");
        }

        [TestMethod]
        public async Task GetChangedFilesVsMergeBaseAsync_ClearsWarnStateWhenMergeBaseFound()
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

            _fakeLogger.ClearWarnMessages();
            await _lister.GetChangedFilesVsMergeBaseAsync(_testRepoPath, _testRepoPath);
            Assert.HasCount(1, _fakeLogger.SnapshotWarnMessages(), "Should warn when default baseline has no merge base");

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Remove("refs/remotes/origin/HEAD");
            }

            _fakeLogger.ClearWarnMessages();
            var withFallbackMergeBase = await _lister.GetChangedFilesVsMergeBaseAsync(_testRepoPath, _testRepoPath);
            Assert.IsNotEmpty(withFallbackMergeBase, "Should find merge base via fallback when origin/HEAD is unset");
            Assert.IsEmpty(_fakeLogger.SnapshotWarnMessages(), "Should not warn when merge base is found");

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Refs.Add("refs/remotes/origin/HEAD", repo.Refs["refs/remotes/origin/main"]);
            }

            _fakeLogger.ClearWarnMessages();
            await _lister.GetChangedFilesVsMergeBaseAsync(_testRepoPath, _testRepoPath);
            await _lister.GetChangedFilesVsMergeBaseAsync(_testRepoPath, _testRepoPath);
            Assert.HasCount(1, _fakeLogger.SnapshotWarnMessages(), "Warn throttle should reset after a successful merge-base lookup");
        }

        [TestMethod]
        public async Task GetChangedFilesVsMergeBaseAsync_WithUnbornFeatureHead_DoesNotWarn()
        {
            ExecGit("checkout --orphan feature-unborn");

            _fakeLogger.ClearWarnMessages();
            await _lister.GetChangedFilesVsMergeBaseAsync(_testRepoPath, _testRepoPath);

            Assert.IsEmpty(
                _fakeLogger.SnapshotWarnMessages(),
                "Unborn feature branch should not emit merge-base warning");
        }

        [TestMethod]
        public void Initialize_SetsGitRootAndWorkspacePath()
        {
            _lister.Initialize(_testRepoPath, new[] { _testRepoPath });

            var fileInRepo = Path.Combine(_testRepoPath, "initialized.cs");
            File.WriteAllText(fileInRepo, "content");

            var result = _lister.GetAllChangedFilesAsync(_testRepoPath, _testRepoPath).Result;

            Assert.IsNotEmpty(result, "Should be able to use lister after initialization");
        }

        [TestMethod]
        public void Dispose_MultipleCalls_DoesNotThrow()
        {
            _lister.Dispose();
            _lister.Dispose();
        }

        [TestMethod]
        public void Dispose_StopsPeriodicScanning()
        {
            _lister.Initialize(_testRepoPath, new[] { _testRepoPath });
            _lister.StartPeriodicScanning(CancellationToken.None);

            _lister.Dispose();

            _lister.StartPeriodicScanning(CancellationToken.None);
        }
    }
}
