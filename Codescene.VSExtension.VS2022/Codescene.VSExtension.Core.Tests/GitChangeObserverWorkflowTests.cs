// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeObserverWorkflowTests : GitChangeObserverTestBase
    {
        protected new string CreateFile(string filename, string content)
        {
            var filePath = Path.Combine(_testRepoPath, filename);
            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content);
            return filePath;
        }

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
    }
}
