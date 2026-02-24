// Copyright (c) CodeScene. All rights reserved.

using LibGit2Sharp;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeObserverWorkflowTests : GitChangeObserverTestBase
    {
        [TestMethod]
        public async Task MultiStepWorkflow_StagingAndUnstagingFiles()
        {
            var file = CreateFile("staging-test.ts", "export const test = 1;");

            using (var repo = new Repository(_testRepoPath))
            {
                LibGit2Sharp.Commands.Stage(repo, "staging-test.ts");
            }

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "staging-test.ts");

            using (var repo = new Repository(_testRepoPath))
            {
                LibGit2Sharp.Commands.Unstage(repo, "staging-test.ts");
            }

            changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "staging-test.ts");

            File.Delete(file);
            changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "staging-test.ts", false);
        }

        [TestMethod]
        public async Task ComplexGit_DetachedHeadState_HandledGracefully()
        {
            string commitSha;
            using (var repo = new Repository(_testRepoPath))
            {
                commitSha = repo.Head.Tip.Sha;
            }

            ExecGit($"checkout {commitSha}");

            var file = CreateFile("detached.ts", "export const detached = 1;");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "detached.ts");

            Assert.IsNotNull(changedFiles, "Should return results even in detached HEAD state");
        }

        [TestMethod]
        public async Task ComplexGit_MergeBranch_DetectsAllChanges()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var branch = repo.CreateBranch("merge-feature");
                LibGit2Sharp.Commands.Checkout(repo, branch);
            }

            CommitFile("merge1.ts", "export const a = 1;", "Add merge1");
            CommitFile("merge2.ts", "export const b = 2;", "Add merge2");

            using (var repo = new Repository(_testRepoPath))
            {
                var mainBranch = repo.Branches["master"] ?? repo.Branches["main"];
                LibGit2Sharp.Commands.Checkout(repo, mainBranch);
            }

            ExecGit("merge merge-feature --no-ff --no-edit");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "merge1.ts", false);
            AssertFileInChangedList(changedFiles, "merge2.ts", false);
        }

        [TestMethod]
        public async Task FileModificationAndRevertCycle_UpdatesCodeHealthMonitor()
        {
            var fileName = "healthy-file.ts";
            var originalContent = "export function hello() { return \"world\"; }";
            var filePath = CommitFile(fileName, originalContent, "Add healthy file");

            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, fileName, false);

            var modifiedContent = "export function hello() { return \"modified\"; }";
            File.WriteAllText(filePath, modifiedContent);

            await TriggerFileChangeAsync(filePath);
            changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, fileName, true);
            AssertFileInTracker(filePath, true);

            File.WriteAllText(filePath, originalContent);

            changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, fileName, false);

            await TriggerFileChangeAsync(filePath);

            AssertFileInTracker(filePath, false);
        }
    }
}
