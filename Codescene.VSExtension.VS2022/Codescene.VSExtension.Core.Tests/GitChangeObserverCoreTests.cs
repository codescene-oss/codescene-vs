// Copyright (c) CodeScene. All rights reserved.

using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeObserverCoreTests : GitChangeObserverTestBase
    {
        [TestMethod]
        public async Task GetChangedFilesVsBaseline_ReturnsEmptyArray_ForCleanRepository()
        {
            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            Assert.IsEmpty(changedFiles, "Should return empty list for clean repository");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_DetectsNewUntrackedFiles()
        {
            CreateFile("test.ts", "console.log(\"test\");");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "test.ts");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_DetectsModifiedFiles()
        {
            var testFile = CommitFile("index.js", "console.log(\"hello\");", "Add index.js");
            File.WriteAllText(testFile, "console.log(\"modified\");");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "index.js");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_DetectsStagedFiles()
        {
            CreateFile("script.py", "print(\"hello\")");

            using (var repo = new Repository(_testRepoPath))
            {
                LibGit2Sharp.Commands.Stage(repo, "script.py");
            }

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "script.py");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_CombinesStatusAndDiffChanges()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var branch = repo.CreateBranch("feature-branch");
                LibGit2Sharp.Commands.Checkout(repo, branch);
            }

            CommitFile("committed.ts", "export const foo = 1;", "Add committed.ts");
            CreateFile("uncommitted.ts", "export const bar = 2;");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "committed.ts");
            AssertFileInChangedList(changedFiles, "uncommitted.ts");
        }

        [DataRow("notes.txt", "Some notes", false, DisplayName = "Rejects unsupported file types (.txt)")]
        [DataRow("code.ts", "export const x = 1;", true, DisplayName = "Accepts supported file types (.ts)")]
        [TestMethod]
        public async Task ShouldProcessFile_ChecksFileSupport(string filename, string content, bool expectedResult)
        {
            var filePath = CreateFile(filename, content);
            _fakeSupportedFileChecker.SetSupported(filePath, expectedResult);

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            var result = _gitChangeObserverCore.ShouldProcessFileForTesting(filePath, changedFiles);

            Assert.AreEqual(
                expectedResult,
                result,
                expectedResult ? $"Should process {Path.GetExtension(filename)} files" : $"Should not process {Path.GetExtension(filename)} files");
        }

        [TestMethod]
        public async Task HandleFileChange_FiltersFilesNotInChangedList()
        {
            var changedFile = CreateFile("changed.ts", "export const x = 1;");
            var committedFile = CommitFile("committed.js", "console.log(\"committed\");", "Add committed.js");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "changed.ts");
            AssertFileInChangedList(changedFiles, "committed.js", false);

            await TriggerFileChangeAsync(changedFile);
            await TriggerFileChangeAsync(committedFile);

            AssertFileInTracker(changedFile);
            AssertFileInTracker(committedFile, false);
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_HandlesFilesWithWhitespaceInNames()
        {
            CreateFile("my file.ts", "console.log(\"has spaces\");");
            CreateFile("test file with spaces.js", "console.log(\"also has spaces\");");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            var fileNames = changedFiles.Select(f => Path.GetFileName(f)).ToList();

            Assert.Contains("my file.ts", fileNames, "Should include file with spaces: my file.ts");
            Assert.Contains("test file with spaces.js", fileNames, "Should include file with spaces: test file with spaces.js");
        }

        [TestMethod]
        public void Dispose_CleansUpFileWatcher()
        {
            Assert.IsNotNull(_gitChangeObserverCore.FileWatcher, "File watcher should exist");

            _gitChangeObserverCore.Dispose();
        }
    }
}
