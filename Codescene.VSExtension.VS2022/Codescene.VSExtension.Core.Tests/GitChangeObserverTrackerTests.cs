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
    }
}
