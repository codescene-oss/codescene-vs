using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Git;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeObserverNaturalEventTests : GitChangeObserverTestBase
    {
        [TestMethod]
        public async Task NaturalEvents_FilesEventuallyTracked()
        {
            _gitChangeObserverCore.Start();
            await Task.Delay(1500);

            var file1 = CreateFile("queued1.ts", "export const a = 1;");
            var file2 = CreateFile("queued2.ts", "export const b = 2;");

            await Task.Delay(100);

            var file1Tracked = await WaitForConditionAsync(
                () =>
                _gitChangeObserverCore.GetTrackerManager().Contains(file1), 5000);
            var file2Tracked = await WaitForConditionAsync(
                () =>
                _gitChangeObserverCore.GetTrackerManager().Contains(file2), 5000);

            Assert.IsTrue(file1Tracked, "File1 should eventually be tracked");
            Assert.IsTrue(file2Tracked, "File2 should eventually be tracked");
        }

        [TestMethod]
        public async Task NaturalEvents_FileModification_DetectedAndTracked()
        {
            _gitChangeObserverCore.Start();
            await Task.Delay(1500);

            var file = CreateFile("modify-test.ts", "export const x = 1;");

            var trackerManager = _gitChangeObserverCore.GetTrackerManager();
            var fileTracked = await WaitForConditionAsync(() => trackerManager.Contains(file), 5000);
            Assert.IsTrue(fileTracked, "File should be tracked after creation");

            File.WriteAllText(file, "export const x = 2;");

            await Task.Delay(2000);

            var stillTracked = trackerManager.Contains(file);
            Assert.IsTrue(stillTracked, "File should still be tracked after modification");
        }
    }
}
