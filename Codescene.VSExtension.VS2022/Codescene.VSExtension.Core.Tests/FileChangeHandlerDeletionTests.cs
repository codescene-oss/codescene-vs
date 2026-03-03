// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class FileChangeHandlerDeletionTests
    {
        private string _testWorkspacePath;
        private FileChangeHandler _handler;
        private FakeLogger _fakeLogger;
        private FakeCodeReviewer _fakeCodeReviewer;
        private FakeSupportedFileChecker _fakeSupportedFileChecker;
        private TrackerManager _trackerManager;

        [TestInitialize]
        public void Setup()
        {
            _testWorkspacePath = Path.Combine(Path.GetTempPath(), $"test-workspace-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testWorkspacePath);

            _fakeLogger = new FakeLogger();
            _fakeCodeReviewer = new FakeCodeReviewer();
            _fakeSupportedFileChecker = new FakeSupportedFileChecker();
            _trackerManager = new TrackerManager();

            _handler = new FileChangeHandler(
                _fakeLogger,
                _fakeCodeReviewer,
                _fakeSupportedFileChecker,
                _testWorkspacePath,
                _trackerManager,
                new FakeGitService());
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testWorkspacePath))
            {
                try
                {
                    Directory.Delete(_testWorkspacePath, true);
                }
                catch
                {
                }
            }
        }

        [TestMethod]
        public async Task HandleFileDeleteAsync_UntrackedFileInChangedList_FiresDeleteEvent()
        {
            var testFile = Path.Combine(_testWorkspacePath, "test.cs");
            var changedFiles = new List<string> { "test.cs" };

            var eventFired = false;
            string? deletedPath = null;
            _handler.FileDeletedFromGit += (sender, e) =>
            {
                eventFired = true;
                deletedPath = e;
            };

            await _handler.HandleFileDeleteAsync(testFile, changedFiles);

            Assert.IsTrue(eventFired);
            Assert.AreEqual(testFile, deletedPath);
        }

        [TestMethod]
        public async Task HandleFileDeleteAsync_UntrackedFileNotInChangedList_NoEventFired()
        {
            var testFile = Path.Combine(_testWorkspacePath, "test.cs");
            var changedFiles = new List<string> { "other.cs" };

            var eventFired = false;
            _handler.FileDeletedFromGit += (sender, e) =>
            {
                eventFired = true;
            };

            await _handler.HandleFileDeleteAsync(testFile, changedFiles);

            Assert.IsFalse(eventFired);
        }

        [TestMethod]
        public async Task HandleFileDeleteAsync_TrackedFile_RemovesFromTrackerAndFiresEvent()
        {
            var testFile = Path.Combine(_testWorkspacePath, "test.cs");
            var changedFiles = new List<string> { "test.cs" };
            _trackerManager.Add(testFile);

            var eventFired = false;
            _handler.FileDeletedFromGit += (sender, e) =>
            {
                eventFired = true;
            };

            await _handler.HandleFileDeleteAsync(testFile, changedFiles);

            Assert.IsFalse(_trackerManager.Contains(testFile));
            Assert.IsTrue(eventFired);
        }

        [TestMethod]
        public async Task HandleFileDeleteAsync_Directory_RemovesAllFilesInDirectory()
        {
            var subdir = Path.Combine(_testWorkspacePath, "subdir");
            var file1 = Path.Combine(subdir, "file1.cs");
            var file2 = Path.Combine(subdir, "file2.cs");

            _trackerManager.Add(file1);
            _trackerManager.Add(file2);

            var deletedFiles = new List<string>();
            _handler.FileDeletedFromGit += (sender, e) =>
            {
                deletedFiles.Add(e);
            };

            var changedFiles = new List<string>();
            await _handler.HandleFileDeleteAsync(subdir, changedFiles);

            Assert.IsFalse(_trackerManager.Contains(file1));
            Assert.IsFalse(_trackerManager.Contains(file2));
            Assert.HasCount(2, deletedFiles);
        }

        [TestMethod]
        public async Task HandleFileDeleteAsync_EventHandlerThrowsException_LogsWarning()
        {
            var testFile = Path.Combine(_testWorkspacePath, "test.cs");
            var changedFiles = new List<string> { "test.cs" };

            _handler.FileDeletedFromGit += (sender, e) =>
            {
                throw new Exception("Test exception");
            };

            await _handler.HandleFileDeleteAsync(testFile, changedFiles);

            Assert.IsTrue(_fakeLogger.WarnMessages.Any(m => m.Contains("Error firing file deleted event")));
        }
    }
}
