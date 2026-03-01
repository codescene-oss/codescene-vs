// Copyright (c) CodeScene. All rights reserved.

using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeObserverGitignoreTests : GitChangeObserverTestBase
    {
        [TestMethod]
        public async Task DeletedFileOnBranchWithGitignore_IsNotInChangedListAndNotReviewed()
        {
            var fileName = "gitignored.ts";
            CommitFile(fileName, "export const x = 1;", "Add file on main");

            using (var repo = new Repository(_testRepoPath))
            {
                var branch = repo.CreateBranch("feature");
                LibGit2Sharp.Commands.Checkout(repo, branch);
            }

            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, fileName + "\n");
            var filePath = Path.Combine(_testRepoPath, fileName);
            File.Delete(filePath);

            using (var repo = new Repository(_testRepoPath))
            {
                LibGit2Sharp.Commands.Stage(repo, new[] { ".gitignore", fileName });
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit("Add to gitignore and remove file", signature, signature);
            }

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, fileName, false);

            var shouldProcess = _gitChangeObserverCore.ShouldProcessFileForTesting(filePath, changedFiles);
            Assert.IsFalse(shouldProcess, "Deleted and ignored file should not be processed for review");
        }

        [TestMethod]
        public async Task GitIgnoredFiles_AreNotTracked()
        {
            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, "*.ignored\n");

            var ignoredFile = CreateFile("secret.ignored", "export const secret = \"hidden\";");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "secret.ignored", false);

            await TriggerFileChangeAsync(ignoredFile);

            AssertFileInTracker(ignoredFile, false);

            File.Delete(gitignorePath);
        }

        [TestMethod]
        public async Task FileBecomesTracked_AfterGitignoreRemoval()
        {
            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, "config.ts\n");

            var ignoredFile = CreateFile("config.ts", "export const config = { secret: true };");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "config.ts", false);

            await TriggerFileChangeAsync(ignoredFile);
            AssertFileInTracker(ignoredFile, false);

            File.Delete(gitignorePath);
            await Task.Delay(100);

            changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "config.ts");

            await TriggerFileChangeAsync(ignoredFile);

            AssertFileInTracker(ignoredFile);
        }
    }
}
