// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeObserverTrackerTests : GitChangeObserverTestBase
    {
        [TestMethod]
        public async Task TrackerTracksAddedFiles()
        {
            var newFile = CreateFile("tracked.ts", "export const x = 1;");
            _gitChangeObserverCore.Start();
            await Task.Delay(500);

            await TriggerFileChangeAsync(newFile);

            AssertFileInTracker(newFile);
        }

        [TestMethod]
        public async Task RemoveFromTracker_RemovesFileFromTracking()
        {
            var newFile = CreateFile("removable.ts", "export const x = 1;");
            _gitChangeObserverCore.Start();
            await Task.Delay(500);
            await TriggerFileChangeAsync(newFile);
            AssertFileInTracker(newFile);

            _gitChangeObserverCore.RemoveFromTracker(newFile);

            AssertFileInTracker(newFile, false);
        }

        [TestMethod]
        public async Task HandleFileDelete_RemovesTrackedFile()
        {
            var newFile = CreateFile("deletable.ts", "export const x = 1;");
            _gitChangeObserverCore.Start();
            await Task.Delay(500);
            await TriggerFileChangeAsync(newFile);
            AssertFileInTracker(newFile);

            File.Delete(newFile);
            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            await _gitChangeObserverCore.HandleFileDeleteForTestingAsync(newFile, changedFiles);

            AssertFileInTracker(newFile, false);
        }

        [TestMethod]
        public async Task HandleFileDelete_RaisesViewUpdateRequestedEvent()
        {
            var eventRaised = false;
            _gitChangeObserverCore.ViewUpdateRequested += (sender, e) => eventRaised = true;

            var newFile = CreateFile("event-test.ts", "export const x = 1;");
            _gitChangeObserverCore.Start();
            await Task.Delay(500);
            await TriggerFileChangeAsync(newFile);
            AssertFileInTracker(newFile);

            File.Delete(newFile);
            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            await _gitChangeObserverCore.HandleFileDeleteForTestingAsync(newFile, changedFiles);

            Assert.IsTrue(eventRaised, "ViewUpdateRequested event should be raised when a tracked file is deleted");
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

            _gitChangeObserverCore.Start();
            await Task.Delay(500);
            await TriggerFileChangeAsync(file1);
            await TriggerFileChangeAsync(file2);
            AssertFileInTracker(file1);
            AssertFileInTracker(file2);

            Directory.Delete(subDir, true);
            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();

            await _gitChangeObserverCore.HandleFileDeleteForTestingAsync(subDir, changedFiles);

            AssertFileInTracker(file1, false);
            AssertFileInTracker(file2, false);
        }

        [TestMethod]
        public async Task HandleFileDelete_FiresDeleteEvent_WhenFileWasOpenInEditorDuringInit()
        {
            var testFile = CreateFile("open-in-editor.cs", "public class Test {}");

            _fakeOpenFilesObserver.AddOpenFile(testFile);

            _fakeGitChangeLister.FilesToReturn = new HashSet<string> { testFile };

            _gitChangeObserverCore.Dispose();
            _gitChangeObserverCore = CreateGitChangeObserverCore();
            await Task.Delay(500);

            AssertFileInTracker(testFile, true);

            var eventRaised = false;
            _gitChangeObserverCore.ViewUpdateRequested += (sender, e) => eventRaised = true;

            File.Delete(testFile);
            var changedFiles = await _gitChangeObserverCore.GetChangedFilesVsBaselineAsync();
            CollectionAssert.AreEqual(new List<string>(), changedFiles, "Changed list should be empty after git stash");

            await _gitChangeObserverCore.HandleFileDeleteForTestingAsync(testFile, changedFiles);

            Assert.IsTrue(eventRaised, "ViewUpdateRequested should be raised when deleting a file that was open in editor during init");
            AssertFileInTracker(testFile, false);
        }
    }
}
