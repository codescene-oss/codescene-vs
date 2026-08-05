// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeListerCpuThrottlingTests : GitChangeDetectorTestBase
    {
        [TestMethod]
        public async Task PeriodicScan_WhenCpuTooBusy_SkipsRun()
        {
            var cpuChecker = new FakeCpuUsageChecker { IsBusy = true };
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                pollingInterval: 5,
                cpuUsageChecker: cpuChecker);

            testableLister.Initialize(_testRepoPath, new[] { _testRepoPath });
            testableLister.StartPeriodicScanning(CancellationToken.None);

            CreateFile("test.cs", "content");

            var filesDetected = new HashSet<string>();
            testableLister.FilesDetected += (sender, files) => filesDetected = files;

            await testableLister.InvokePeriodicScanAsync();

            Assert.IsEmpty(filesDetected, "Should not detect files when CPU is too busy");
            var infoMessages = _fakeLogger.SnapshotInfoMessages();
            Assert.IsTrue(
                infoMessages.Exists(m => m.Contains("CPU usage too high")),
                "Should log that scan was skipped due to high CPU");

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_WhenCpuNotBusy_ProcessesFiles()
        {
            var cpuChecker = new FakeCpuUsageChecker { IsBusy = false };
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                pollingInterval: 5,
                cpuUsageChecker: cpuChecker);

            testableLister.Initialize(_testRepoPath, new[] { _testRepoPath });
            testableLister.StartPeriodicScanning(CancellationToken.None);

            CreateFile("test.cs", "content");

            var filesDetected = new HashSet<string>();
            testableLister.FilesDetected += (sender, files) => filesDetected = files;

            await testableLister.InvokePeriodicScanAsync();

            Assert.IsNotEmpty(filesDetected, "Should detect files when CPU is not busy");
            Assert.IsTrue(filesDetected.Any(f => f.Contains("test.cs")), "Should contain the test file");

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_WithNullCpuChecker_ProcessesFiles()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                pollingInterval: 5,
                cpuUsageChecker: null);

            testableLister.Initialize(_testRepoPath, new[] { _testRepoPath });
            testableLister.StartPeriodicScanning(CancellationToken.None);

            CreateFile("test.cs", "content");

            var filesDetected = new HashSet<string>();
            testableLister.FilesDetected += (sender, files) => filesDetected = files;

            await testableLister.InvokePeriodicScanAsync();

            Assert.IsNotEmpty(filesDetected, "Should detect files when no CPU checker is provided");

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_CpuCheckHappensAfterOtherChecks()
        {
            var cpuChecker = new FakeCpuUsageChecker { IsBusy = true };
            var ideActivityTracker = new FakeIdeActivityTracker();
            ideActivityTracker.SetActiveForTesting(false);

            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                pollingInterval: 5,
                ideActivityTracker: ideActivityTracker,
                cpuUsageChecker: cpuChecker);

            testableLister.Initialize(_testRepoPath, new[] { _testRepoPath });
            testableLister.StartPeriodicScanning(CancellationToken.None);

            CreateFile("test.cs", "content");

            await testableLister.InvokePeriodicScanAsync();

            Assert.AreEqual(0, cpuChecker.CheckCount, "CPU checker should not be called if IDE is not active (earlier check fails first)");

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_WhenCpuBusyThenNotBusy_SecondRunProcessesFiles()
        {
            var cpuChecker = new FakeCpuUsageChecker { IsBusy = true };
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                pollingInterval: 5,
                cpuUsageChecker: cpuChecker);

            testableLister.Initialize(_testRepoPath, new[] { _testRepoPath });
            testableLister.StartPeriodicScanning(CancellationToken.None);

            CreateFile("test.cs", "content");

            var filesDetected = new HashSet<string>();
            testableLister.FilesDetected += (sender, files) => filesDetected = files;

            await testableLister.InvokePeriodicScanAsync();
            Assert.IsEmpty(filesDetected, "First run should be skipped due to high CPU");

            cpuChecker.IsBusy = false;
            filesDetected.Clear();

            await testableLister.InvokePeriodicScanAsync();
            Assert.IsNotEmpty(filesDetected, "Second run should process files when CPU is available");

            testableLister.Dispose();
        }

        private class FakeCpuUsageChecker : ICpuUsageChecker
        {
            public bool IsBusy { get; set; }

            public int CheckCount { get; private set; }

            public Task<bool> IsCpuTooBusyAsync()
            {
                CheckCount++;
                return Task.FromResult(IsBusy);
            }
        }
    }
}
