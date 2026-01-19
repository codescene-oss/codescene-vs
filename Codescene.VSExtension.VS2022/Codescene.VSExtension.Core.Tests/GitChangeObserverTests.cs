using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.VS2022.Application.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class GitChangeObserverTests
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
            _fakeSavedFilesTracker = new FakeSavedFilesTracker();
            _fakeOpenFilesObserver = new FakeOpenFilesObserver();

            _gitChangeObserver = CreateGitChangeObserver();

            Thread.Sleep(500);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _gitChangeObserver?.Dispose();

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

        private GitChangeObserver CreateGitChangeObserver()
        {
            var observer = new GitChangeObserver();

            var loggerField = typeof(GitChangeObserver).GetField("_logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var reviewerField = typeof(GitChangeObserver).GetField("_codeReviewer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var checkerField = typeof(GitChangeObserver).GetField("_supportedFileChecker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var gitServiceField = typeof(GitChangeObserver).GetField("_gitService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            loggerField?.SetValue(observer, _fakeLogger);
            reviewerField?.SetValue(observer, _fakeCodeReviewer);
            checkerField?.SetValue(observer, _fakeSupportedFileChecker);
            gitServiceField?.SetValue(observer, _fakeGitService);

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
            File.WriteAllText(filePath, content);
            return filePath;
        }

        private string CommitFile(string filename, string content, string message)
        {
            var filePath = CreateFile(filename, content);

            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, filename);
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit(message, signature, signature);
            }

            return filePath;
        }

        private HashSet<string> GetTracker()
        {
            var trackerField = typeof(GitChangeObserver).GetField("_tracker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (HashSet<string>)trackerField?.GetValue(_gitChangeObserver);
        }

        private async Task TriggerFileChangeAsync(string filePath)
        {
            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();

            var handleFileChangeMethod = typeof(GitChangeObserver).GetMethod("HandleFileChangeAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task)handleFileChangeMethod?.Invoke(_gitChangeObserver, new object[] { filePath, changedFiles });
            await task;
        }

        private void AssertFileInChangedList(List<string> changedFiles, string filename, bool shouldExist = true)
        {
            var exists = changedFiles.Any(f => f.EndsWith(filename, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(shouldExist, exists, shouldExist ? $"Should include {filename}" : $"Should not include {filename}");
        }

        private void AssertFileInTracker(string filePath, bool shouldExist = true)
        {
            var tracker = GetTracker();
            var exists = tracker.Contains(filePath);
            Assert.AreEqual(shouldExist, exists, shouldExist ? "File should be in tracker" : "File should not be in tracker");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_ReturnsEmptyArray_ForCleanRepository()
        {
            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();

            Assert.AreEqual(0, changedFiles.Count, "Should return empty list for clean repository");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_DetectsNewUntrackedFiles()
        {
            CreateFile("test.ts", "console.log(\"test\");");

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "test.ts");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_DetectsModifiedFiles()
        {
            var testFile = CommitFile("index.js", "console.log(\"hello\");", "Add index.js");
            File.WriteAllText(testFile, "console.log(\"modified\");");

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "index.js");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_DetectsStagedFiles()
        {
            CreateFile("script.py", "print(\"hello\")");

            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, "script.py");
            }

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "script.py");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_CombinesStatusAndDiffChanges()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var branch = repo.CreateBranch("feature-branch");
                Commands.Checkout(repo, branch);
            }

            CommitFile("committed.ts", "export const foo = 1;", "Add committed.ts");
            CreateFile("uncommitted.ts", "export const bar = 2;");

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();

            AssertFileInChangedList(changedFiles, "committed.ts");
            AssertFileInChangedList(changedFiles, "uncommitted.ts");
        }

        [TestMethod]
        public async Task TrackerTracksAddedFiles()
        {
            var newFile = CreateFile("tracked.ts", "export const x = 1;");
            _gitChangeObserver.Start();
            await Task.Delay(500);

            await TriggerFileChangeAsync(newFile);

            AssertFileInTracker(newFile);
        }

        [TestMethod]
        public async Task RemoveFromTracker_RemovesFileFromTracking()
        {
            var newFile = CreateFile("removable.ts", "export const x = 1;");
            _gitChangeObserver.Start();
            await Task.Delay(500);
            await TriggerFileChangeAsync(newFile);
            AssertFileInTracker(newFile);

            _gitChangeObserver.RemoveFromTracker(newFile);

            AssertFileInTracker(newFile, false);
        }

        [TestMethod]
        public async Task HandleFileDelete_RemovesTrackedFile()
        {
            var newFile = CreateFile("deletable.ts", "export const x = 1;");
            _gitChangeObserver.Start();
            await Task.Delay(500);
            await TriggerFileChangeAsync(newFile);
            AssertFileInTracker(newFile);

            File.Delete(newFile);
            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();

            var handleFileDeleteMethod = typeof(GitChangeObserver).GetMethod("HandleFileDeleteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task)handleFileDeleteMethod?.Invoke(_gitChangeObserver, new object[] { newFile, changedFiles });
            await task;

            AssertFileInTracker(newFile, false);
        }

        [TestMethod]
        public async Task HandleFileDelete_HandlesDirectoryDeletion()
        {
            var subDir = Path.Combine(_testRepoPath, "subdir");
            Directory.CreateDirectory(subDir);
            var file1 = Path.Combine(subDir, "file1.ts");
            var file2 = Path.Combine(subDir, "file2.ts");
            File.WriteAllText(file1, "export const a = 1;");
            File.WriteAllText(file2, "export const b = 2;");

            _gitChangeObserver.Start();
            await Task.Delay(500);
            await TriggerFileChangeAsync(file1);
            await TriggerFileChangeAsync(file2);
            AssertFileInTracker(file1);
            AssertFileInTracker(file2);

            Directory.Delete(subDir, true);
            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();

            var handleFileDeleteMethod = typeof(GitChangeObserver).GetMethod("HandleFileDeleteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var task = (Task)handleFileDeleteMethod?.Invoke(_gitChangeObserver, new object[] { subDir, changedFiles });
            await task;

            AssertFileInTracker(file1, false);
            AssertFileInTracker(file2, false);
        }

        [TestMethod]
        public async Task ShouldProcessFile_RejectsUnsupportedFileTypes()
        {
            var txtFile = CreateFile("notes.txt", "Some notes");
            _fakeSupportedFileChecker.SetSupported(txtFile, false);

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
            var shouldProcessFileMethod = typeof(GitChangeObserver).GetMethod("ShouldProcessFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (bool)shouldProcessFileMethod?.Invoke(_gitChangeObserver, new object[] { txtFile, changedFiles });

            Assert.IsFalse(result, "Should not process .txt files");
        }

        [TestMethod]
        public async Task ShouldProcessFile_AcceptsSupportedFileTypes()
        {
            var tsFile = CreateFile("code.ts", "export const x = 1;");
            _fakeSupportedFileChecker.SetSupported(tsFile, true);

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
            var shouldProcessFileMethod = typeof(GitChangeObserver).GetMethod("ShouldProcessFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (bool)shouldProcessFileMethod?.Invoke(_gitChangeObserver, new object[] { tsFile, changedFiles });

            Assert.IsTrue(result, "Should process .ts files");
        }

        [TestMethod]
        public async Task HandleFileChange_FiltersFilesNotInChangedList()
        {
            var changedFile = CreateFile("changed.ts", "export const x = 1;");
            var committedFile = CommitFile("committed.js", "console.log(\"committed\");", "Add committed.js");

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
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

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
            var fileNames = changedFiles.Select(f => Path.GetFileName(f)).ToList();

            Assert.IsTrue(fileNames.Contains("my file.ts"), "Should include file with spaces: my file.ts");
            Assert.IsTrue(fileNames.Contains("test file with spaces.js"), "Should include file with spaces: test file with spaces.js");
        }

        [TestMethod]
        public async Task GitIgnoredFiles_AreNotTracked()
        {
            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, "*.ignored\n");

            var ignoredFile = CreateFile("secret.ignored", "export const secret = \"hidden\";");

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
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

            var changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "config.ts", false);

            await TriggerFileChangeAsync(ignoredFile);
            AssertFileInTracker(ignoredFile, false);

            File.Delete(gitignorePath);
            await Task.Delay(100);

            changedFiles = await _gitChangeObserver.GetChangedFilesVsBaselineAsync();
            AssertFileInChangedList(changedFiles, "config.ts");

            await TriggerFileChangeAsync(ignoredFile);

            AssertFileInTracker(ignoredFile);
        }

        [TestMethod]
        public void Dispose_CleansUpFileWatcher()
        {
            var fileWatcherField = typeof(GitChangeObserver).GetField("_fileWatcher", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(fileWatcherField?.GetValue(_gitChangeObserver), "File watcher should exist");

            _gitChangeObserver.Dispose();

            Assert.IsTrue(true, "Dispose completed without errors");
        }

        [TestMethod]
        public async Task EventsAreQueued_InsteadOfProcessedImmediately()
        {
            var file1 = CreateFile("queued1.ts", "export const a = 1;");
            var file2 = CreateFile("queued2.ts", "export const b = 2;");

            _gitChangeObserver.Start();
            await Task.Delay(500);

            var eventQueueField = typeof(GitChangeObserver).GetField("_eventQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var queue = eventQueueField.GetValue(_gitChangeObserver);

            var fileChangeEventType = typeof(GitChangeObserver).Assembly.GetType("Codescene.VSExtension.VS2022.Application.Git.FileChangeEvent");
            var fileChangeTypeEnum = typeof(GitChangeObserver).Assembly.GetType("Codescene.VSExtension.VS2022.Application.Git.FileChangeType");

            var createType = Enum.Parse(fileChangeTypeEnum, "Create");
            var event1 = Activator.CreateInstance(fileChangeEventType, createType, file1);
            var event2 = Activator.CreateInstance(fileChangeEventType, createType, file2);

            var enqueueMethod = queue.GetType().GetMethod("Enqueue");
            enqueueMethod.Invoke(queue, new[] { event1 });
            enqueueMethod.Invoke(queue, new[] { event2 });

            var countProperty = queue.GetType().GetProperty("Count");
            Assert.AreEqual(2, (int)countProperty.GetValue(queue), "Events should be queued");
            AssertFileInTracker(file1, false);
            AssertFileInTracker(file2, false);

            await Task.Delay(1500);

            Assert.AreEqual(0, (int)countProperty.GetValue(queue), "Queue should be empty after processing");
            AssertFileInTracker(file1);
            AssertFileInTracker(file2);
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaseline_CalledOncePerBatch_NotPerFile()
        {
            var observer = new TestableGitChangeObserver();

            var loggerField = typeof(GitChangeObserver).GetField("_logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var reviewerField = typeof(GitChangeObserver).GetField("_codeReviewer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var checkerField = typeof(GitChangeObserver).GetField("_supportedFileChecker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var gitServiceField = typeof(GitChangeObserver).GetField("_gitService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            loggerField?.SetValue(observer, _fakeLogger);
            reviewerField?.SetValue(observer, _fakeCodeReviewer);
            checkerField?.SetValue(observer, _fakeSupportedFileChecker);
            gitServiceField?.SetValue(observer, _fakeGitService);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            await Task.Delay(1000);

            observer.ResetCallCount();

            var files = new[]
            {
                CreateFile("cache1.ts", "export const a = 1;"),
                CreateFile("cache2.ts", "export const b = 2;"),
                CreateFile("cache3.ts", "export const c = 3;")
            };

            observer.Start();
            await Task.Delay(500);

            var eventQueueField = typeof(GitChangeObserver).GetField("_eventQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var queue = eventQueueField.GetValue(observer);

            var fileChangeEventType = typeof(GitChangeObserver).Assembly.GetType("Codescene.VSExtension.VS2022.Application.Git.FileChangeEvent");
            var fileChangeTypeEnum = typeof(GitChangeObserver).Assembly.GetType("Codescene.VSExtension.VS2022.Application.Git.FileChangeType");
            var createType = Enum.Parse(fileChangeTypeEnum, "Create");
            var enqueueMethod = queue.GetType().GetMethod("Enqueue");

            foreach (var file in files)
            {
                var evt = Activator.CreateInstance(fileChangeEventType, createType, file);
                enqueueMethod.Invoke(queue, new[] { evt });
            }

            Assert.AreEqual(0, observer.GetChangedFilesCallCount, "Method should not be called until batch processing starts");

            await Task.Delay(1500);

            Assert.AreEqual(1, observer.GetChangedFilesCallCount, "GetChangedFilesVsBaselineAsync should be called exactly once per batch");

            var trackerField = typeof(GitChangeObserver).GetField("_tracker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var tracker = (HashSet<string>)trackerField?.GetValue(observer);
            foreach (var file in files)
            {
                Assert.IsTrue(tracker.Contains(file), $"File {file} should be in tracker");
            }

            observer.Dispose();
        }

        [TestMethod]
        public async Task EmptyQueue_DoesNotTrigger_UnnecessaryProcessing()
        {
            var observer = new TestableGitChangeObserver();

            var loggerField = typeof(GitChangeObserver).GetField("_logger", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var reviewerField = typeof(GitChangeObserver).GetField("_codeReviewer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var checkerField = typeof(GitChangeObserver).GetField("_supportedFileChecker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var gitServiceField = typeof(GitChangeObserver).GetField("_gitService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            loggerField?.SetValue(observer, _fakeLogger);
            reviewerField?.SetValue(observer, _fakeCodeReviewer);
            checkerField?.SetValue(observer, _fakeSupportedFileChecker);
            gitServiceField?.SetValue(observer, _fakeGitService);

            observer.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            await Task.Delay(1000);

            observer.Start();
            await Task.Delay(500);

            observer.ResetCallCount();

            await Task.Delay(5000);

            Assert.AreEqual(0, observer.GetChangedFilesCallCount, "GetChangedFilesVsBaselineAsync should not be called when queue is empty");

            observer.Dispose();
        }

        [TestMethod]
        public void Dispose_CleansUpScheduledTimer()
        {
            _gitChangeObserver.Start();

            var timerField = typeof(GitChangeObserver).GetField("_scheduledTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(timerField.GetValue(_gitChangeObserver), "Scheduled timer should exist before disposal");

            _gitChangeObserver.Dispose();

            Assert.IsNull(timerField.GetValue(_gitChangeObserver), "Scheduled timer should be null after disposal");
        }

        #region Fake Implementations

        private class FakeLogger : Codescene.VSExtension.Core.Application.Services.ErrorHandling.ILogger
        {
            public void Debug(string message) { }
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message, Exception ex) { }
        }

        private class FakeCodeReviewer : Codescene.VSExtension.Core.Application.Services.CodeReviewer.ICodeReviewer
        {
            public Core.Models.ReviewModels.FileReviewModel Review(string path, string content)
            {
                return new Core.Models.ReviewModels.FileReviewModel { FilePath = path };
            }

            public Core.Models.Cli.Delta.DeltaResponseModel Delta(Core.Models.ReviewModels.FileReviewModel review, string currentCode)
            {
                return null;
            }
        }

        private class FakeSupportedFileChecker : ISupportedFileChecker
        {
            private readonly Dictionary<string, bool> _supported = new Dictionary<string, bool>();

            public bool IsSupported(string filePath)
            {
                if (_supported.ContainsKey(filePath))
                {
                    return _supported[filePath];
                }

                var extension = Path.GetExtension(filePath)?.ToLower();
                return extension == ".ts" || extension == ".js" || extension == ".py" || extension == ".cs";
            }

            public void SetSupported(string filePath, bool isSupported)
            {
                _supported[filePath] = isSupported;
            }
        }

        private class FakeGitService : Codescene.VSExtension.Core.Application.Services.Git.IGitService
        {
            public string GetFileContentForCommit(string path)
            {
                return string.Empty;
            }

            public string GetBranchCreationCommit(string path, Repository repository)
            {
                return string.Empty;
            }
        }

        private class FakeSavedFilesTracker : ISavedFilesTracker
        {
            private readonly HashSet<string> _savedFiles = new HashSet<string>();

            public IEnumerable<string> GetSavedFiles()
            {
                return _savedFiles;
            }

            public void AddSavedFile(string filePath)
            {
                _savedFiles.Add(filePath);
            }
        }

        private class FakeOpenFilesObserver : IOpenFilesObserver
        {
            private readonly HashSet<string> _openFiles = new HashSet<string>();

            public IEnumerable<string> GetAllVisibleFileNames()
            {
                return _openFiles;
            }

            public void AddOpenFile(string filePath)
            {
                _openFiles.Add(filePath);
            }
        }

        private class TestableGitChangeObserver : GitChangeObserver
        {
            public int GetChangedFilesCallCount { get; private set; }

            public void ResetCallCount()
            {
                GetChangedFilesCallCount = 0;
            }

            public override async Task<List<string>> GetChangedFilesVsBaselineAsync()
            {
                GetChangedFilesCallCount++;
                return await base.GetChangedFilesVsBaselineAsync();
            }
        }

        #endregion
    }
}
