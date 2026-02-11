// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeObserverGitignoreTests : GitChangeObserverTestBase
    {
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
