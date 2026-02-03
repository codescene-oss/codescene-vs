// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Util;
using FileModel = Codescene.VSExtension.Core.Models.WebComponent.Data.File;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class DeltaJobTrackerTests
    {
        [TestInitialize]
        public void Setup()
        {
            // Clear any existing jobs before each test
            ClearAllJobs();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clear static state between tests
            ClearAllJobs();
        }

        [TestMethod]
        public void Add_NewJob_FiresJobStartedEvent()
        {
            var job = CreateJob();
            var received = CaptureEventJob(h => DeltaJobTracker.JobStarted += h, () => DeltaJobTracker.Add(job));

            Assert.IsNotNull(received);
            Assert.AreEqual(job, received);
        }

        [TestMethod]
        public void Add_NewJob_AppearsInRunningJobs()
        {
            var job = CreateJob();
            DeltaJobTracker.Add(job);
            Assert.IsTrue(DeltaJobTracker.RunningJobs.Contains(job));
        }

        [TestMethod]
        public void Add_DuplicateJob_DoesNotFireEventTwice()
        {
            var job = CreateJob();
            var count = CountEvents(h => DeltaJobTracker.JobStarted += h, () =>
            {
                DeltaJobTracker.Add(job);
                DeltaJobTracker.Add(job);
            });
            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void Add_MultipleJobs_AllAppearInRunningJobs()
        {
            var job1 = CreateJob("file1.cs");
            var job2 = CreateJob("file2.cs");

            DeltaJobTracker.Add(job1);
            DeltaJobTracker.Add(job2);

            Assert.HasCount(2, DeltaJobTracker.RunningJobs);
            Assert.IsTrue(DeltaJobTracker.RunningJobs.Contains(job1));
            Assert.IsTrue(DeltaJobTracker.RunningJobs.Contains(job2));
        }

        [TestMethod]
        public void Remove_ExistingJob_FiresJobFinishedEvent()
        {
            var job = CreateJob();
            DeltaJobTracker.Add(job);
            var received = CaptureEventJob(h => DeltaJobTracker.JobFinished += h, () => DeltaJobTracker.Remove(job));

            Assert.IsNotNull(received);
            Assert.AreEqual(job, received);
        }

        [TestMethod]
        public void Remove_ExistingJob_RemovedFromRunningJobs()
        {
            var job = CreateJob();
            DeltaJobTracker.Add(job);
            DeltaJobTracker.Remove(job);
            Assert.IsFalse(DeltaJobTracker.RunningJobs.Contains(job));
        }

        [TestMethod]
        public void Remove_NonExistentJob_DoesNotFireEvent()
        {
            var job = CreateJob();
            var count = CountEvents(h => DeltaJobTracker.JobFinished += h, () => DeltaJobTracker.Remove(job));
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void Remove_AlreadyRemovedJob_DoesNotFireEventTwice()
        {
            var job = CreateJob();
            DeltaJobTracker.Add(job);
            var count = CountEvents(h => DeltaJobTracker.JobFinished += h, () =>
            {
                DeltaJobTracker.Remove(job);
                DeltaJobTracker.Remove(job);
            });
            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public void RunningJobs_ReturnsSnapshot_NotLiveCollection()
        {
            // Arrange
            var job = CreateJob();
            DeltaJobTracker.Add(job);
            var snapshot = DeltaJobTracker.RunningJobs;

            // Act
            DeltaJobTracker.Remove(job);

            // Assert - original snapshot still contains the job
            Assert.IsTrue(snapshot.Contains(job));

            // But current RunningJobs does not
            Assert.IsFalse(DeltaJobTracker.RunningJobs.Contains(job));
        }

        [TestMethod]
        public void RunningJobs_WhenEmpty_ReturnsEmptyCollection()
        {
            // Assert
            Assert.IsEmpty(DeltaJobTracker.RunningJobs);
        }

        [TestMethod]
        public void RunningJobs_IsReadOnly()
        {
            // Arrange
            var jobs = DeltaJobTracker.RunningJobs;

            // Assert
            Assert.IsInstanceOfType(jobs, typeof(IReadOnlyCollection<Job>));
        }

        private static void ClearAllJobs()
        {
            foreach (var job in DeltaJobTracker.RunningJobs.ToList())
            {
                DeltaJobTracker.Remove(job);
            }
        }

        private static Job CreateJob(string fileName = "test.cs", string type = "deltaAnalysis") =>
            new Job { Type = type, State = "running", File = new FileModel { FileName = fileName } };

        private static int CountEvents(Action<Action<Job>> subscribe, Action action)
        {
            int count = 0;
            subscribe(_ => count++);
            action();
            return count;
        }

        private static Job? CaptureEventJob(Action<Action<Job>> subscribe, Action action)
        {
            Job? captured = null;
            subscribe(job => captured = job);
            action();
            return captured;
        }
    }
}
