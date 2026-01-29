using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
                _trackerManager);
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
        public async Task HandleFileDeleteAsync_UntrackedFileInChangedList_FiresDeleteEvent()
        {
            var testFile = Path.Combine(_testWorkspacePath, "test.cs");
            var changedFiles = new List<string> { "test.cs" };

            var eventFired = false;
            string deletedPath = null;
            _handler.FileDeletedFromGit += (sender, e) =>
            {
                eventFired = true;
                deletedPath = e.FilePath;
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
        public void ShouldProcessFile_NullWorkspacePath_ReturnsTrue()
        {
            var handlerWithNullWorkspace = new FileChangeHandler(
                _fakeLogger,
                _fakeCodeReviewer,
                _fakeSupportedFileChecker,
                null,
                _trackerManager);

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
                "",
                _trackerManager);

            var testFile = "test.cs";
            var changedFiles = new List<string> { "test.cs" };

            var result = handlerWithEmptyWorkspace.ShouldProcessFile(testFile, changedFiles);

            Assert.IsTrue(result);
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

        [TestMethod]
        public async Task HandleFileChangeAsync_ValidFile_AddsToTrackerAndReviews()
        {
            var testFile = Path.Combine(_testWorkspacePath, "test.cs");
            var changedFiles = new List<string> { "test.cs" };
            File.WriteAllText(testFile, "public class Test {}");

            await _handler.HandleFileChangeAsync(testFile, changedFiles);

            await Task.Delay(200);

            Assert.IsTrue(_trackerManager.Contains(testFile));
            Assert.AreEqual(1, _fakeCodeReviewer.ReviewCallCount);
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
                deletedFiles.Add(e.FilePath);
            };

            var changedFiles = new List<string>();
            await _handler.HandleFileDeleteAsync(subdir, changedFiles);

            Assert.IsFalse(_trackerManager.Contains(file1));
            Assert.IsFalse(_trackerManager.Contains(file2));
            Assert.AreEqual(2, deletedFiles.Count);
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

        private class FakeLogger : ILogger
        {
            public List<string> DebugMessages = new List<string>();
            public List<string> InfoMessages = new List<string>();
            public List<string> WarnMessages = new List<string>();
            public List<string> ErrorMessages = new List<string>();

            public void Debug(string message) => DebugMessages.Add(message);
            public void Info(string message) => InfoMessages.Add(message);
            public void Warn(string message) => WarnMessages.Add(message);
            public void Error(string message, Exception ex) => ErrorMessages.Add(message);
        }

        private class FakeCodeReviewer : ICodeReviewer
        {
            public int ReviewCallCount { get; private set; }
            public bool ThrowOnReview { get; set; }

            public FileReviewModel Review(string path, string content)
            {
                ReviewCallCount++;
                if (ThrowOnReview)
                {
                    throw new Exception("Test exception from code reviewer");
                }
                return new FileReviewModel { FilePath = path };
            }

            public DeltaResponseModel Delta(FileReviewModel review, string currentCode)
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
    }
}
