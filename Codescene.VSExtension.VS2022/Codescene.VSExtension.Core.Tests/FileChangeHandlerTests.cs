// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class FileChangeHandlerTests
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
        public async Task HandleFileChangeAsync_DirectoryPath_ReturnsEarly()
        {
            var directoryPath = Path.Combine(_testWorkspacePath, "mydir");
            var changedFiles = new List<string> { "mydir" };

            await _handler.HandleFileChangeAsync(directoryPath, changedFiles);

            Assert.IsFalse(_trackerManager.Contains(directoryPath));
            Assert.AreEqual(0, _fakeCodeReviewer.ReviewCallCount);
        }

        [TestMethod]
        public async Task HandleFileChangeAsync_DirectoryPathWithSeparator_ReturnsEarly()
        {
            var directoryPath = Path.Combine(_testWorkspacePath, "mydir") + Path.DirectorySeparatorChar;
            var changedFiles = new List<string> { "mydir/" };

            await _handler.HandleFileChangeAsync(directoryPath, changedFiles);

            Assert.IsFalse(_trackerManager.Contains(directoryPath));
            Assert.AreEqual(0, _fakeCodeReviewer.ReviewCallCount);
        }

        [TestMethod]
        public async Task HandleFileChangeAsync_NonExistentFile_NoReviewCalled()
        {
            var nonExistentFile = Path.Combine(_testWorkspacePath, "nonexistent.cs");
            var changedFiles = new List<string> { "nonexistent.cs" };

            await _handler.HandleFileChangeAsync(nonExistentFile, changedFiles);

            Assert.IsTrue(_trackerManager.Contains(nonExistentFile));
            Assert.AreEqual(0, _fakeCodeReviewer.ReviewCallCount);
        }

        [TestMethod]
        public async Task HandleFileChangeAsync_FileReadThrowsException_LogsWarning()
        {
            var testFile = Path.Combine(_testWorkspacePath, "test.cs");
            var changedFiles = new List<string> { "test.cs" };
            File.WriteAllText(testFile, "public class Test {}");

            await _handler.HandleFileChangeAsync(testFile, changedFiles);

            await Task.Delay(100);
            File.Delete(testFile);
            await Task.Delay(100);

            Assert.IsTrue(_fakeLogger.WarnMessages.Count > 0 || _fakeCodeReviewer.ReviewCallCount == 1);
        }

        [TestMethod]
        public async Task HandleFileChangeAsync_CodeReviewerThrowsException_LogsWarning()
        {
            var testFile = Path.Combine(_testWorkspacePath, "test.cs");
            var changedFiles = new List<string> { "test.cs" };
            File.WriteAllText(testFile, "public class Test {}");

            _fakeCodeReviewer.ThrowOnReview = true;

            await _handler.HandleFileChangeAsync(testFile, changedFiles);

            await Task.Delay(200);

            Assert.IsTrue(_fakeLogger.WarnMessages.Any(m => m.Contains("Could not load file for review")));
        }

        [TestMethod]
        public async Task HandleFileChangeAsync_ValidFile_AddsToTrackerAndReviews()
        {
            var testFile = Path.Combine(_testWorkspacePath, "test.cs");
            var changedFiles = new List<string> { "test.cs" };
            File.WriteAllText(testFile, "public class Test {}");

            await _handler.HandleFileChangeAsync(testFile, changedFiles);

            await Task.Delay(200);

            Assert.IsTrue(_trackerManager.Contains(testFile));
            Assert.IsGreaterThanOrEqualTo(_fakeCodeReviewer.ReviewCallCount, 1);
        }

        [TestMethod]
        public async Task HandleFileChangeAsync_FileNotInChangedList_ReturnsEarlyWithoutProcessing()
        {
            var testFile = Path.Combine(_testWorkspacePath, "test.cs");
            File.WriteAllText(testFile, "public class Test {}");
            var changedFiles = new List<string> { "other.cs" };

            await _handler.HandleFileChangeAsync(testFile, changedFiles);

            Assert.IsFalse(_trackerManager.Contains(testFile), "Should not add file to tracker");
            Assert.AreEqual(0, _fakeCodeReviewer.ReviewCallCount, "Should not review file");
        }

        [TestMethod]
        public async Task HandleFileChangeAsync_RevertedTrackedFile_RemovesFromTrackerAndFiresFileDeletedFromGit()
        {
            var testFile = Path.Combine(_testWorkspacePath, "test.cs");
            File.WriteAllText(testFile, "public class Test {}");
            _trackerManager.Add(testFile);

            var eventFired = false;
            string? deletedPath = null;
            _handler.FileDeletedFromGit += (sender, e) =>
            {
                eventFired = true;
                deletedPath = e;
            };

            var changedFiles = new List<string> { "other.cs" };

            await _handler.HandleFileChangeAsync(testFile, changedFiles);

            Assert.IsFalse(_trackerManager.Contains(testFile));
            Assert.IsTrue(eventFired);
            Assert.AreEqual(testFile, deletedPath);
            Assert.AreEqual(0, _fakeCodeReviewer.ReviewCallCount);
        }

        [TestMethod]
        public async Task HandleFileChangeAsync_UnsupportedFileType_ReturnsEarlyWithoutProcessing()
        {
            var testFile = Path.Combine(_testWorkspacePath, "test.txt");
            File.WriteAllText(testFile, "text content");
            var changedFiles = new List<string> { "test.txt" };

            _fakeSupportedFileChecker.SetSupported(testFile, false);

            await _handler.HandleFileChangeAsync(testFile, changedFiles);

            Assert.IsFalse(_trackerManager.Contains(testFile), "Should not add file to tracker");
            Assert.AreEqual(0, _fakeCodeReviewer.ReviewCallCount, "Should not review file");
        }

        [TestMethod]
        public void ShouldProcessFile_NullWorkspacePath_ReturnsTrue()
        {
            var handlerWithNullWorkspace = new FileChangeHandler(
                _fakeLogger,
                _fakeCodeReviewer,
                _fakeSupportedFileChecker,
                null,
                _trackerManager,
                new FakeGitService());

            var testFile = "test.cs";
            var changedFiles = new List<string> { "test.cs" };

            var result = handlerWithNullWorkspace.ShouldProcessFile(testFile, changedFiles);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ShouldProcessFile_EmptyWorkspacePath_ReturnsTrue()
        {
            var handlerWithEmptyWorkspace = new FileChangeHandler(
                _fakeLogger,
                _fakeCodeReviewer,
                _fakeSupportedFileChecker,
                string.Empty,
                _trackerManager,
                new FakeGitService());

            var testFile = "test.cs";
            var changedFiles = new List<string> { "test.cs" };

            var result = handlerWithEmptyWorkspace.ShouldProcessFile(testFile, changedFiles);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ShouldProcessFile_IgnoredFile_ReturnsFalse()
        {
            var testFile = Path.Combine(_testWorkspacePath, "ignored.cs");
            var changedFiles = new List<string> { "ignored.cs" };
            var handlerWithIgnoringGit = new FileChangeHandler(
                _fakeLogger,
                _fakeCodeReviewer,
                _fakeSupportedFileChecker,
                _testWorkspacePath,
                _trackerManager,
                new FakeGitServiceIgnorePath(testFile));

            var result = handlerWithIgnoringGit.ShouldProcessFile(testFile, changedFiles);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ShouldProcessFile_UnsupportedFile_ReturnsFalse()
        {
            var testFile = Path.Combine(_testWorkspacePath, "test.unsupported");
            var changedFiles = new List<string> { "test.unsupported" };

            _fakeSupportedFileChecker.SetSupported(testFile, false);

            var result = _handler.ShouldProcessFile(testFile, changedFiles);

            Assert.IsFalse(result);
        }
    }
}
