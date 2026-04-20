// Copyright (c) CodeScene. All rights reserved.

using System.Reflection;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Models;
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

        [TestMethod]
        public void Dispose_UnsubscribesAllWatcherHandlers_WhenStarted()
        {
            _gitChangeObserverCore.Start();
            _gitChangeObserverCore.Dispose();
        }

        [TestMethod]
        public void FilesDetected_AfterDispose_DoesNotThrow()
        {
            _gitChangeObserverCore.Dispose();
            var files = new HashSet<string> { Path.Combine(_testRepoPath, "any.cs") };
            _fakeGitChangeLister.SimulateFilesDetected(files);
        }

        [TestMethod]
        public void OnGitChangeListerFilesDetected_WhenDisposed_ReturnsWithoutThrowing()
        {
            _gitChangeObserverCore.Dispose();
            var method = typeof(GitChangeObserverCore).GetMethod("OnGitChangeListerFilesDetected", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(_gitChangeObserverCore, new object[] { this, new HashSet<string>() });
        }

        [TestMethod]
        public void OnCodeHealthRulesChanged_RemovesAllTrackedFiles()
        {
            var existingFile = CreateFile("rules-invalidate.ts", "export const x = 1;");
            _fakeGitChangeLister.SimulateFilesDetected(new HashSet<string> { existingFile });
            AssertFileInTracker(existingFile, true);

            var method = typeof(GitChangeObserverCore).GetMethod("OnCodeHealthRulesChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(_gitChangeObserverCore, new object[] { this, EventArgs.Empty });

            AssertFileInTracker(existingFile, false);
        }

        [TestMethod]
        public void OnGitChangeListerFilesDetected_WhenTokenCancelled_DoesNotProcessFiles()
        {
            _gitChangeObserverCore.Start();
            var ctsField = typeof(GitChangeObserverCore).GetField("_cts", BindingFlags.NonPublic | BindingFlags.Instance);
            var cts = (CancellationTokenSource)ctsField?.GetValue(_gitChangeObserverCore);
            cts?.Cancel();
            var filePath = Path.Combine(_testRepoPath, "cancelled.ts");
            _fakeGitChangeLister.SimulateFilesDetected(new HashSet<string> { filePath });
            AssertFileInTracker(filePath, false);
        }

        [TestMethod]
        public void Initialize_WithNullSolutionPath_DoesNotCreateWatcher()
        {
            _gitChangeObserverCore.Dispose();
            _gitChangeObserverCore = new GitChangeObserverCore(
                _fakeLogger,
                _fakeCodeReviewer,
                _fakeSupportedFileChecker,
                _fakeTaskScheduler,
                _fakeGitChangeLister,
                _fakeGitService);
            _gitChangeObserverCore.Initialize(null, _fakeSavedFilesTracker, _fakeOpenFilesObserver, null);
            Assert.IsNull(_gitChangeObserverCore.FileWatcher);
        }

        [TestMethod]
        public void InitializeTracker_WhenCollectFilesThrows_LogsError()
        {
            _fakeGitChangeLister.ThrowOnCollectFiles = true;
            _fakeLogger.ErrorMessages.Clear();
            _gitChangeObserverCore.Dispose();
            _gitChangeObserverCore = CreateGitChangeObserverCore();
            Assert.IsTrue(
                _fakeLogger.ErrorMessages.Exists(m => m.Item1.Contains("Error initializing tracker")),
                "Should log when CollectFilesFromRepoStateAsync throws");
        }

        [TestMethod]
        public void OnGitChangeListerFilesDetected_WhenGetChangedFilesThrows_LogsWarning()
        {
            _gitChangeObserverCore.Dispose();
            _gitChangeObserverCore = new GitChangeObserverCore(
                _fakeLogger,
                _fakeCodeReviewer,
                _fakeSupportedFileChecker,
                _fakeTaskScheduler,
                _fakeGitChangeLister,
                _fakeGitService);
            _gitChangeObserverCore.Initialize(_testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver, getChangedFilesCallback: () => Task.FromException<List<string>>(new InvalidOperationException("simulated")));
            _fakeLogger.ErrorMessages.Clear();
            _fakeGitChangeLister.SimulateFilesDetected(new HashSet<string> { Path.Combine(_testRepoPath, "x.cs") });
            Assert.IsTrue(
                _fakeLogger.ErrorMessages.Exists(m => m.Item1.Contains("Error processing detected files")),
                "Should log when getChangedFiles callback throws");
        }

        [TestMethod]
        public void ShouldEnqueueEvent_WithExtensionAndNotIgnored_ReturnsTrue()
        {
            var method = typeof(GitChangeObserverCore).GetMethod("ShouldEnqueueEvent", BindingFlags.NonPublic | BindingFlags.Instance);
            var args = new FileSystemEventArgs(WatcherChangeTypes.Created, _testRepoPath, "file.cs");
            var result = method.Invoke(_gitChangeObserverCore, new object[] { args });
            Assert.IsTrue((bool)result);
        }

        [TestMethod]
        public void CancelAndReset_ReconnectsEventProcessor()
        {
            _gitChangeObserverCore.Start();
            _gitChangeObserverCore.CancelAndReset();
            var existingFile = CreateFile("after-reset.ts", "x");
            _fakeGitChangeLister.SimulateFilesDetected(new HashSet<string> { existingFile });
            AssertFileInTracker(existingFile, true);
        }

        [TestMethod]
        public void OnGitChangeListerFilesDetected_AddsExistingFilesToTracker()
        {
            var existingFile = CreateFile("tracked.ts", "export const x = 1;");
            var files = new HashSet<string> { existingFile };

            _fakeGitChangeLister.SimulateFilesDetected(files);

            AssertFileInTracker(existingFile, true);
        }

        [TestMethod]
        public void OnGitChangeListerFilesDetected_SkipsNonExistentFiles()
        {
            var nonExistentFile = Path.Combine(_testRepoPath, "nonexistent.ts");
            var files = new HashSet<string> { nonExistentFile };

            _fakeGitChangeLister.SimulateFilesDetected(files);

            AssertFileInTracker(nonExistentFile, false);
        }

        [TestMethod]
        public async Task OnGitChangeListerFilesDetected_WhenExceptionThrown_LogsError()
        {
            _fakeLogger.ErrorMessages.Clear();

            var eventHandler = typeof(GitChangeObserverCore).GetMethod("OnGitChangeListerFilesDetected", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var exceptionThrown = false;
            try
            {
                eventHandler?.Invoke(_gitChangeObserverCore, new object[] { this, null });
            }
            catch
            {
                exceptionThrown = true;
            }

            Assert.IsFalse(exceptionThrown, "Exception should be caught and logged, not thrown");

            await WaitForConditionAsync(() => _fakeLogger.ErrorMessages.Count > 0, 2000);

            Assert.IsNotEmpty(_fakeLogger.ErrorMessages, "Should log error when exception is thrown");
            Assert.IsTrue(
                _fakeLogger.ErrorMessages.Exists(msg => msg.Item1.Contains("Error processing detected files")),
                "Error should mention error processing detected files");
        }

        [TestMethod]
        public async Task ProcessDetectedFileQueueAsync_WhenCancelledDuringProcessing_CompletesSilently()
        {
            _gitChangeObserverCore.Dispose();
            _gitChangeObserverCore = new GitChangeObserverCore(
                _fakeLogger,
                _fakeCodeReviewer,
                _fakeSupportedFileChecker,
                _fakeTaskScheduler,
                _fakeGitChangeLister,
                _fakeGitService);
            _gitChangeObserverCore.Initialize(
                _testRepoPath,
                _fakeSavedFilesTracker,
                _fakeOpenFilesObserver,
                getChangedFilesCallback: () => Task.FromCanceled<List<string>>(new CancellationToken(canceled: true)));
            _fakeLogger.ErrorMessages.Clear();

            var method = typeof(GitChangeObserverCore).GetMethod("ProcessDetectedFileQueueAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            var task = (Task)method.Invoke(_gitChangeObserverCore, new object[] { Path.Combine(_testRepoPath, "cancelled.ts"), CancellationToken.None });

            await task;

            Assert.IsFalse(
                _fakeLogger.ErrorMessages.Exists(msg => msg.Item1.Contains("Error processing detected files")),
                "Cancellation should be swallowed without error logs");
        }

        [TestMethod]
        public async Task OnGitChangeListerFilesDetected_WhenProcessingFailsForFile_SubsequentDetectionRestartsWorker()
        {
            var existingFile = CreateFile("retry.ts", "export const x = 1;");
            var getChangedFilesCallCount = 0;

            _gitChangeObserverCore.Dispose();
            _gitChangeObserverCore = new GitChangeObserverCore(
                _fakeLogger,
                _fakeCodeReviewer,
                _fakeSupportedFileChecker,
                new BackgroundAsyncTaskScheduler(),
                _fakeGitChangeLister,
                _fakeGitService);
            _gitChangeObserverCore.Initialize(
                _testRepoPath,
                _fakeSavedFilesTracker,
                _fakeOpenFilesObserver,
                getChangedFilesCallback: () =>
                {
                    if (Interlocked.Increment(ref getChangedFilesCallCount) == 1)
                    {
                        return Task.FromException<List<string>>(new InvalidOperationException("simulated"));
                    }

                    return Task.FromResult(new List<string> { existingFile });
                });
            _fakeLogger.WarnMessages.Clear();

            _fakeGitChangeLister.SimulateFilesDetected(new HashSet<string> { existingFile });
            var firstAttemptStarted = await WaitForConditionAsync(
                () => getChangedFilesCallCount >= 1,
                2000);
            Assert.IsTrue(firstAttemptStarted, "First processing attempt should start for the detected file.");

            _fakeGitChangeLister.SimulateFilesDetected(new HashSet<string> { existingFile });
            var retryProcessed = await WaitForConditionAsync(
                () => getChangedFilesCallCount >= 2,
                5000);

            Assert.IsTrue(retryProcessed, "A later detection for the same file should start a new worker after a failure.");
        }

        [TestMethod]
        public void UpdateWorkspacePaths_UpdatesHandlerAndLister()
        {
            var paths = new[] { _testRepoPath };
            _gitChangeObserverCore.UpdateWorkspacePaths(paths);
            _gitChangeObserverCore.UpdateWorkspacePaths(null);
            _gitChangeObserverCore.UpdateWorkspacePaths(Array.Empty<string>());
        }

        [TestMethod]
        public void RemoveFromTracker_RemovesFile()
        {
            var path = CreateFile("track.cs", "x");
            _fakeGitChangeLister.SimulateFilesDetected(new HashSet<string> { path });
            AssertFileInTracker(path, true);
            _gitChangeObserverCore.RemoveFromTracker(path);
            AssertFileInTracker(path, false);
        }

        [TestMethod]
        public void GetTrackerManager_ReturnsNonNull()
        {
            var manager = _gitChangeObserverCore.GetTrackerManager();
            Assert.IsNotNull(manager);
        }

        [TestMethod]
        public void EnsureWatcherHandlersBound_CalledTwice_ReturnsEarlySecondTime()
        {
            _gitChangeObserverCore.Start();
            var method = typeof(GitChangeObserverCore).GetMethod("EnsureWatcherHandlersBound", BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(_gitChangeObserverCore, null);
        }

        [TestMethod]
        public async Task HandleFileChange_AddsDeltaJobAndRemovesWhenComplete()
        {
            var testFile = CreateFile("test.cs", "public class Test {}");
            var jobsStarted = new List<Job>();
            var jobsFinished = new List<Job>();

            Action<Job> onJobStarted = (job) => jobsStarted.Add(job);
            Action<Job> onJobFinished = (job) => jobsFinished.Add(job);

            Codescene.VSExtension.Core.Util.DeltaJobTracker.JobStarted += onJobStarted;
            Codescene.VSExtension.Core.Util.DeltaJobTracker.JobFinished += onJobFinished;

            try
            {
                await TriggerFileChangeAsync(testFile);

                await WaitForConditionAsync(() => jobsStarted.Count > 0 && jobsFinished.Count > 0, 2000);

                Assert.HasCount(1, jobsStarted, "Should add exactly one delta job");
                Assert.HasCount(1, jobsFinished, "Should remove exactly one delta job");
                Assert.AreEqual(testFile, jobsStarted[0].File.FileName, "Job should track the correct file");
                Assert.AreEqual(testFile, jobsFinished[0].File.FileName, "Finished job should be the same file");

                var runningJobs = Codescene.VSExtension.Core.Util.DeltaJobTracker.RunningJobs;
                Assert.IsEmpty(runningJobs, "No jobs should be running after completion");
            }
            finally
            {
                Codescene.VSExtension.Core.Util.DeltaJobTracker.JobStarted -= onJobStarted;
                Codescene.VSExtension.Core.Util.DeltaJobTracker.JobFinished -= onJobFinished;
            }
        }
    }
}
