// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeListerTests : GitChangeDetectorTestBase
    {
        private GitChangeLister _lister;

        [TestInitialize]
        public void SetupLister()
        {
            _lister = new GitChangeLister(_fakeSavedFilesTracker, _fakeSupportedFileChecker, _fakeLogger);
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
    }
}
