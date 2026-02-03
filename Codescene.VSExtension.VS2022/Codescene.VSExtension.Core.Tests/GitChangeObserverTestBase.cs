// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    public abstract class GitChangeObserverTestBase
    {
        protected string _testRepoPath;
        protected GitChangeObserverCore _gitChangeObserverCore;
        protected FakeLogger _fakeLogger;
        protected FakeCodeReviewer _fakeCodeReviewer;
        protected FakeSupportedFileChecker _fakeSupportedFileChecker;
        protected FakeGitService _fakeGitService;
        protected FakeAsyncTaskScheduler _fakeTaskScheduler;
        protected FakeSavedFilesTracker _fakeSavedFilesTracker;
        protected FakeOpenFilesObserver _fakeOpenFilesObserver;

        [TestInitialize]
        public void Setup()
        {
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-git-repo-observer-{Guid.NewGuid()}");

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
            _fakeTaskScheduler = new FakeAsyncTaskScheduler();
            _fakeSavedFilesTracker = new FakeSavedFilesTracker();
            _fakeOpenFilesObserver = new FakeOpenFilesObserver();

            _gitChangeObserverCore = CreateGitChangeObserverCore();

            Thread.Sleep(500);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _gitChangeObserverCore?.Dispose();

            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            if (File.Exists(gitignorePath))
            {
                File.Delete(gitignorePath);
            }

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

        protected GitChangeObserverCore CreateGitChangeObserverCore()
        {
            var observer = new GitChangeObserverCore(
                _fakeLogger,
                _fakeCodeReviewer,
                _fakeSupportedFileChecker,
                _fakeGitService,
                _fakeTaskScheduler);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            return observer;
        }

        protected GitChangeObserverCore CreateGitChangeObserver()
        {
            return CreateGitChangeObserverCore();
        }

        protected void ExecGit(string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = _testRepoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
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

        protected string CreateFile(TestFileData fileData)
        {
            var filePath = Path.Combine(_testRepoPath, fileData.Filename);
            File.WriteAllText(filePath, fileData.Content);
            return filePath;
        }

        protected string CreateFile(string filename, string content)
        {
            return CreateFile(new TestFileData(filename, content));
        }

        protected string CommitFile(TestFileData fileData)
        {
            var filePath = CreateFile(fileData);

            using (var repo = new Repository(_testRepoPath))
            {
                LibGit2Sharp.Commands.Stage(repo, fileData.Filename);
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit(fileData.CommitMessage, signature, signature);
            }

            return filePath;
        }

        protected string CommitFile(string filename, string content, string message)
        {
            return CommitFile(new TestFileData(filename, content, message));
        }

        protected TrackerManager GetTrackerManager()
        {
            return _gitChangeObserverCore.GetTrackerManager();
        }

        protected async Task TriggerFileChangeAsync(string filePath)
        {
            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            await _gitChangeObserverCore.HandleFileChangeForTestingAsync(filePath, changedFiles);
        }

        protected FileAssertionHelper CreateAssertionHelper(List<string> changedFiles)
        {
            return new FileAssertionHelper(changedFiles, GetTrackerManager());
        }

        protected void AssertFileInChangedList(List<string> changedFiles, string filename, bool shouldExist = true)
        {
            CreateAssertionHelper(changedFiles).AssertInChangedList(filename, shouldExist);
        }

        protected void AssertFileInTracker(string filePath, bool shouldExist = true)
        {
            CreateAssertionHelper(null).AssertInTracker(filePath, shouldExist);
        }

        protected async Task<bool> WaitForConditionAsync(Func<bool> condition, int timeoutMs = 5000, int pollIntervalMs = 100)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                {
                    return true;
                }

                await Task.Delay(pollIntervalMs);
            }

            return false;
        }
    }
}
