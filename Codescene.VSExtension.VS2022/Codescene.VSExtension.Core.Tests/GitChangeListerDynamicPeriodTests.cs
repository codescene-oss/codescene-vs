// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeListerDynamicPeriodTests : GitChangeDetectorTestBase
    {
        [TestMethod]
        public async Task PeriodicScan_WhenSlowExecution_IncreasesPeriod()
        {
            var basePeriodSeconds = 1;
            var simulatedDuration = TimeSpan.FromSeconds(2);
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                pollingInterval: basePeriodSeconds);

            testableLister.Initialize(_testRepoPath, new[] { _testRepoPath });
            testableLister.StartPeriodicScanning(CancellationToken.None);

            var executor = testableLister.GetScheduledExecutorForTesting();
            Assert.IsNotNull(executor, "Scheduled executor should exist after StartPeriodicScanning");
            var initialPeriod = executor.GetInterval();
            Assert.AreEqual(TimeSpan.FromSeconds(basePeriodSeconds), initialPeriod);

            CreateFile("test.cs", "content");
            testableLister.SimulatedScanDuration = simulatedDuration;

            await testableLister.InvokePeriodicScanAsync();

            var newPeriod = executor.GetInterval();
            var minExpectedPeriod = (basePeriodSeconds * 2) + simulatedDuration.TotalSeconds;
            Assert.IsGreaterThan(
                initialPeriod.TotalSeconds,
                newPeriod.TotalSeconds,
                $"Period should be increased from {initialPeriod.TotalSeconds}s, got {newPeriod.TotalSeconds}s");
            Assert.IsGreaterThanOrEqualTo(
                minExpectedPeriod,
                newPeriod.TotalSeconds,
                $"Period should be at least {minExpectedPeriod}s, got {newPeriod.TotalSeconds}s");

            var infoMessages = _fakeLogger.SnapshotDebugMessages();
            Assert.IsTrue(
                infoMessages.Exists(m => m.Contains("completed in")),
                "Should log completion time");

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_WhenPeriodAlreadyHigher_DoesNotDecrease()
        {
            var basePeriodSeconds = 1;
            var simulatedDuration = TimeSpan.FromSeconds(2);
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                pollingInterval: basePeriodSeconds);

            testableLister.Initialize(_testRepoPath, new[] { _testRepoPath });
            testableLister.StartPeriodicScanning(CancellationToken.None);

            var executor = testableLister.GetScheduledExecutorForTesting();
            Assert.IsNotNull(executor);

            var highPeriod = TimeSpan.FromSeconds(50);
            executor.SetInterval(highPeriod);
            Assert.AreEqual(highPeriod, executor.GetInterval());

            CreateFile("test.cs", "content");
            testableLister.SimulatedScanDuration = simulatedDuration;

            await testableLister.InvokePeriodicScanAsync();

            Assert.AreEqual(highPeriod, executor.GetInterval(), "Period should not decrease");

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_WhenFastExecution_KeepsBasePeriod()
        {
            var basePeriodSeconds = 5;
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                pollingInterval: basePeriodSeconds);

            testableLister.Initialize(_testRepoPath, new[] { _testRepoPath });
            testableLister.StartPeriodicScanning(CancellationToken.None);

            var executor = testableLister.GetScheduledExecutorForTesting();
            Assert.IsNotNull(executor);
            var initialPeriod = executor.GetInterval();
            Assert.AreEqual(TimeSpan.FromSeconds(basePeriodSeconds), initialPeriod);

            CreateFile("test.cs", "content");

            await testableLister.InvokePeriodicScanAsync();

            Assert.AreEqual(initialPeriod, executor.GetInterval(), "Period should remain unchanged for fast execution");

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_WhenExecutorNotStarted_DoesNotThrow()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                pollingInterval: 1);

            testableLister.Initialize(_testRepoPath, new[] { _testRepoPath });

            CreateFile("test.cs", "content");
            testableLister.SimulatedScanDuration = TimeSpan.FromSeconds(2);

            await testableLister.InvokePeriodicScanAsync();

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_LogsCompletionTime()
        {
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                pollingInterval: 5);

            testableLister.Initialize(_testRepoPath, new[] { _testRepoPath });
            testableLister.StartPeriodicScanning(CancellationToken.None);

            CreateFile("test.cs", "content");

            await testableLister.InvokePeriodicScanAsync();

            var debugMessages = _fakeLogger.SnapshotDebugMessages();
            Assert.IsTrue(
                debugMessages.Exists(m => m.Contains("completed in") && m.Contains("s")),
                "Should log completion time in debug messages");

            testableLister.Dispose();
        }

        [TestMethod]
        public async Task PeriodicScan_WhenExceedsThreshold_LogsIncreaseMessage()
        {
            var basePeriodSeconds = 1;
            var simulatedDuration = TimeSpan.FromSeconds(2);
            var testableLister = new TestableGitChangeLister(
                _fakeSavedFilesTracker,
                _fakeSupportedFileChecker,
                _fakeLogger,
                _fakeGitService,
                pollingInterval: basePeriodSeconds);

            testableLister.Initialize(_testRepoPath, new[] { _testRepoPath });
            testableLister.StartPeriodicScanning(CancellationToken.None);

            CreateFile("test.cs", "content");
            testableLister.SimulatedScanDuration = simulatedDuration;

            await testableLister.InvokePeriodicScanAsync();

            var infoMessages = _fakeLogger.SnapshotInfoMessages();
            Assert.IsTrue(
                infoMessages.Exists(m => m.Contains("increased period to")),
                "Should log when period is increased");

            testableLister.Dispose();
        }
    }
}
