// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class SavedFilesTrackerCoreTests
    {
        private SavedFilesTrackerCore _tracker;
        private FakeDocumentSaveEventSource _fakeEventSource;
        private FakeOpenFilesObserver _fakeOpenFilesObserver;
        private FakeLogger _fakeLogger;

        [TestInitialize]
        public void Setup()
        {
            _fakeEventSource = new FakeDocumentSaveEventSource();
            _fakeOpenFilesObserver = new FakeOpenFilesObserver();
            _fakeLogger = new FakeLogger();

            _tracker = new SavedFilesTrackerCore(_fakeEventSource, _fakeOpenFilesObserver, _fakeLogger);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _tracker?.Dispose();
        }

        [TestMethod]
        public void GetSavedFiles_ReturnsDefensiveCopy()
        {
            _fakeOpenFilesObserver.AddOpenFile(@"C:\test\file1.cs");
            _fakeOpenFilesObserver.AddOpenFile(@"C:\test\file2.cs");
            _fakeEventSource.SimulateSave(@"C:\test\file1.cs");
            _fakeEventSource.SimulateSave(@"C:\test\file2.cs");

            var result1 = _tracker.GetSavedFiles();
            var result2 = _tracker.GetSavedFiles();

            Assert.AreNotSame(result1, result2, "Should return different instances");

            var list1 = result1.ToList();
            var list2 = result2.ToList();

            Assert.HasCount(2, list1, "First result should have 2 files");
            Assert.HasCount(2, list2, "Second result should have 2 files");

            Assert.Contains(@"C:\test\file1.cs", list1, "First result should contain file1.cs");
            Assert.Contains(@"C:\test\file2.cs", list1, "First result should contain file2.cs");
            Assert.Contains(@"C:\test\file1.cs", list2, "Second result should contain file1.cs");
            Assert.Contains(@"C:\test\file2.cs", list2, "Second result should contain file2.cs");

            var beforeCount = _tracker.GetSavedFiles().Count();
            list1.Clear();
            var afterCount = _tracker.GetSavedFiles().Count();

            Assert.AreEqual(beforeCount, afterCount, "Modifying returned list should not affect internal state");
            Assert.AreEqual(2, afterCount, "Internal state should still have 2 files after clearing returned list");
        }

        [TestMethod]
        //       isVisible  expectedCount
        [DataRow(true,      1,             DisplayName = "Tracks visible files")]
        [DataRow(false,     0,             DisplayName = "Does not track invisible files")]
        public void OnDocumentSaved_TracksBasedOnVisibility(bool isVisible, int expectedCount)
        {
            var filePath = @"C:\test\file.cs";

            if (isVisible)
            {
                _fakeOpenFilesObserver.AddOpenFile(filePath);
            }

            _fakeEventSource.SimulateSave(filePath);

            var result = _tracker.GetSavedFiles();

            Assert.AreEqual(expectedCount, result.Count());
        }

        [TestMethod]
        public void ClearSavedFiles_RemovesAllTrackedFiles()
        {
            _fakeOpenFilesObserver.AddOpenFile(@"C:\test\file1.cs");
            _fakeOpenFilesObserver.AddOpenFile(@"C:\test\file2.cs");
            _fakeEventSource.SimulateSave(@"C:\test\file1.cs");
            _fakeEventSource.SimulateSave(@"C:\test\file2.cs");

            Assert.AreEqual(2, _tracker.GetSavedFiles().Count());

            _tracker.ClearSavedFiles();

            Assert.AreEqual(0, _tracker.GetSavedFiles().Count(), "Should have no tracked files after clear");
        }

        [TestMethod]
        public void RemoveFromTracker_RemovesSpecificFile()
        {
            _fakeOpenFilesObserver.AddOpenFile(@"C:\test\file1.cs");
            _fakeOpenFilesObserver.AddOpenFile(@"C:\test\file2.cs");
            _fakeEventSource.SimulateSave(@"C:\test\file1.cs");
            _fakeEventSource.SimulateSave(@"C:\test\file2.cs");

            _tracker.RemoveFromTracker(@"C:\test\file1.cs");

            var result = _tracker.GetSavedFiles().ToList();
            Assert.HasCount(1, result);
            Assert.AreEqual(@"C:\test\file2.cs", result[0]);
        }

        [TestMethod]
        public void OnDocumentSaved_IgnoresNullOrEmptyPath()
        {
            _fakeEventSource.SimulateSave(null);
            _fakeEventSource.SimulateSave(string.Empty);

            var result = _tracker.GetSavedFiles();

            Assert.AreEqual(0, result.Count(), "Should ignore null or empty paths");
        }

        [TestMethod]
        public void RemoveFromTracker_HandlesNullOrEmptyPath()
        {
            _fakeOpenFilesObserver.AddOpenFile(@"C:\test\file1.cs");
            _fakeEventSource.SimulateSave(@"C:\test\file1.cs");

            _tracker.RemoveFromTracker(null);
            _tracker.RemoveFromTracker(string.Empty);

            Assert.AreEqual(1, _tracker.GetSavedFiles().Count(), "Should still have the original file");
        }

        [TestMethod]
        public void GetSavedFiles_ThreadSafe()
        {
            _fakeOpenFilesObserver.AddOpenFile(@"C:\test\file.cs");

            var tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    _fakeEventSource.SimulateSave(@"C:\test\file.cs");
                    _ = _tracker.GetSavedFiles().ToList();
                }));
            }

            Task.WaitAll(tasks.ToArray());

            var result = _tracker.GetSavedFiles();
            Assert.AreEqual(1, result.Count(), "Should have exactly one tracked file despite concurrent access");
        }

        [TestMethod]
        public void OnDocumentSaved_IsCaseInsensitive()
        {
            _fakeOpenFilesObserver.AddOpenFile(@"C:\TEST\File.cs");
            _fakeEventSource.SimulateSave(@"C:\test\file.cs");

            var result = _tracker.GetSavedFiles();

            Assert.AreEqual(1, result.Count(), "Should track file with case-insensitive matching");
        }

        [TestMethod]
        public void OnDocumentSaved_DoesNotDuplicateFiles()
        {
            _fakeOpenFilesObserver.AddOpenFile(@"C:\test\file.cs");
            _fakeEventSource.SimulateSave(@"C:\test\file.cs");
            _fakeEventSource.SimulateSave(@"C:\test\file.cs");
            _fakeEventSource.SimulateSave(@"C:\TEST\FILE.CS");

            var result = _tracker.GetSavedFiles();

            Assert.AreEqual(1, result.Count(), "Should not duplicate files with case-insensitive comparison");
        }

        [TestMethod]
        public void Start_CallsEventSourceStart()
        {
            Assert.IsFalse(_fakeEventSource.StartCalled, "Start should not be called initially");

            _tracker.Start();

            Assert.IsTrue(_fakeEventSource.StartCalled, "Start should be called on event source");
        }

        [TestMethod]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            _fakeOpenFilesObserver.AddOpenFile(@"C:\test\file.cs");
            _fakeEventSource.SimulateSave(@"C:\test\file.cs");

            _tracker.Dispose();
            _tracker.Dispose();
        }
    }

    public class FakeDocumentSaveEventSource : IDocumentSaveEventSource
    {
        public event EventHandler<string> DocumentSaved;

        public bool StartCalled { get; private set; }

        public void Start()
        {
            StartCalled = true;
        }

        public void SimulateSave(string filePath)
        {
            DocumentSaved?.Invoke(this, filePath);
        }

        public void Dispose()
        {
        }
    }
}
