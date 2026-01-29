using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.VS2022.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Tests
{
    [TestClass]
    public class GitChangeObserverWorkflowTests
    {
        private string _testRepoPath;
        private GitChangeObserver _gitChangeObserver;
        private FakeLogger _fakeLogger;
        private FakeCodeReviewer _fakeCodeReviewer;
        private FakeSupportedFileChecker _fakeSupportedFileChecker;
        private FakeGitService _fakeGitService;
        private FakeSavedFilesTracker _fakeSavedFilesTracker;
        private FakeOpenFilesObserver _fakeOpenFilesObserver;

        [TestInitialize]
        public void Setup()
        {
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-git-repo-workflow-{Guid.NewGuid()}");

            if (Directory.Exists(_testRepoPath))
            {
                Directory.Delete(_testRepoPath, true);
            }

            Directory.CreateDirectory(_testRepoPath);

            Repository.Init(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Config.Set("user.email", "test@example.com");
                repo.Config.Set("user.name", "Test User");
                repo.Config.Set("advice.defaultBranchName", "false");
            }

            var gitInfoDir = Path.Combine(_testRepoPath, ".git", "info");
            Directory.CreateDirectory(gitInfoDir);
            var dummyExcludesPath = Path.Combine(gitInfoDir, "exclude-test");
            File.WriteAllText(dummyExcludesPath, "# Test excludes file - will not match anything\n__xxxxxxxxxxxxx__\n");

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Config.Set("core.excludesfile", dummyExcludesPath);
            }

            CommitFile("README.md", "# Test Repository", "Initial commit");

            _fakeLogger = new FakeLogger();
            _fakeCodeReviewer = new FakeCodeReviewer();
            _fakeSupportedFileChecker = new FakeSupportedFileChecker();
            _fakeGitService = new FakeGitService();
            _fakeSavedFilesTracker = new FakeSavedFilesTracker();
            _fakeOpenFilesObserver = new FakeOpenFilesObserver();

            _gitChangeObserver = CreateGitChangeObserver();

            Thread.Sleep(500);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _gitChangeObserver?.Dispose();

            if (Directory.Exists(_testRepoPath))
            {
                try
                {
                    Directory.Delete(_testRepoPath, true);
                }
                catch
                {
                }
            }
        }

        private GitChangeObserver CreateGitChangeObserver()
        {
            var observer = new GitChangeObserver(_fakeLogger, _fakeCodeReviewer,
                _fakeSupportedFileChecker, _fakeGitService);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            return observer;
        }

        private void ExecGit(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = _testRepoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
            {
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var error = process.StandardError.ReadToEnd();
                    throw new Exception($"Git command failed: {args}\n{error}");
                }
            }
        }

        private string CreateFile(string filename, string content)
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

        private string CommitFile(string filename, string content, string message)
        {
            var filePath = CreateFile(filename, content);

            using (var repo = new Repository(_testRepoPath))
            {
                LibGit2Sharp.Commands.Stage(repo, filename);
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit(message, signature, signature);
            }

            return filePath;
        }

        private void AssertFileInChangedList(List<string> changedFiles, string filename, bool shouldExist = true)
        {
            var exists = changedFiles.Any(f => f.EndsWith(filename, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(shouldExist, exists, shouldExist ? $"Should include {filename}" : $"Should not include {filename}");
        }

        private void AssertFileInTracker(string filePath, bool shouldExist = true)
        {
            var trackerManager = _gitChangeObserver.GetTrackerManager();
            var exists = trackerManager.Contains(filePath);
            Assert.AreEqual(shouldExist, exists, shouldExist ? "File should be in tracker" : "File should not be in tracker");
        }

        private async Task TriggerFileChangeAsync(string filePath)
        {
            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
            await _gitChangeObserver.HandleFileChangeForTestingAsync(filePath, changedFiles);
        }

        [TestMethod]
        public async Task MultiStepWorkflow_StagingAndUnstagingFiles()
        {
            var file = CreateFile("staging-test.ts", "export const test = 1;");

            using (var repo = new Repository(_testRepoPath))
            {
                LibGit2Sharp.Commands.Stage(repo, "staging-test.ts");
            }

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "staging-test.ts");

            using (var repo = new Repository(_testRepoPath))
            {
                LibGit2Sharp.Commands.Unstage(repo, "staging-test.ts");
            }

            changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "staging-test.ts");

            File.Delete(file);
            changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
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

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
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

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "merge1.ts", false);
            AssertFileInChangedList(changedFiles, "merge2.ts", false);
        }

    }
}
