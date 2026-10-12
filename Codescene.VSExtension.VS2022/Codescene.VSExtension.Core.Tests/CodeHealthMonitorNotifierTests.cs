// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Services;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class CodeHealthMonitorNotifierTests
    {
        private CodeHealthMonitorNotifier _notifier;

        [TestInitialize]
        public void Setup()
        {
            DeltaJobTracker.Clear();
            _notifier = new CodeHealthMonitorNotifier();
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var job in DeltaJobTracker.RunningJobs)
            {
                DeltaJobTracker.Remove(job);
            }
        }

        [TestMethod]
        public void OnDeltaStarting_RaisesViewUpdateRequested()
        {
            var eventFired = false;
            _notifier.ViewUpdateRequested += (s, e) => eventFired = true;

            _notifier.OnDeltaStarting("file.cs");

            Assert.IsTrue(eventFired);
        }

        [TestMethod]
        public void OnDeltaStarting_CalledTwiceForSameFile_RemovesPreviousJobFromTracker()
        {
            _notifier.OnDeltaStarting("file.cs");
            var jobsAfterFirst = DeltaJobTracker.RunningJobs.Count;

            _notifier.OnDeltaStarting("file.cs");
            var jobsAfterSecond = DeltaJobTracker.RunningJobs.Count;

            Assert.AreEqual(1, jobsAfterFirst);
            Assert.AreEqual(1, jobsAfterSecond);
        }

        [TestMethod]
        public void OnDeltaCompleted_WithExistingJob_RaisesViewUpdateRequested()
        {
            _notifier.OnDeltaStarting("file.cs");
            var eventFired = false;
            _notifier.ViewUpdateRequested += (s, e) => eventFired = true;

            _notifier.OnDeltaCompleted("file.cs");

            Assert.IsTrue(eventFired);
        }

        [TestMethod]
        public void OnDeltaCompleted_WithUnknownFile_DoesNotRaiseEvent()
        {
            var eventFired = false;
            _notifier.ViewUpdateRequested += (s, e) => eventFired = true;

            _notifier.OnDeltaCompleted("unknown.cs");

            Assert.IsFalse(eventFired);
        }

        [TestMethod]
        public void RequestViewUpdate_RaisesViewUpdateRequested()
        {
            var eventFired = false;
            _notifier.ViewUpdateRequested += (s, e) => eventFired = true;

            _notifier.RequestViewUpdate();

            Assert.IsTrue(eventFired);
        }

        [TestMethod]
        public void RequestViewUpdate_WithNoSubscribers_DoesNotThrow()
        {
            _notifier.RequestViewUpdate();
        }

        [TestMethod]
        public void ApplyQueue_MapsRunningAndQueuedJobs()
        {
            _notifier.ApplyQueue(new ReviewQueue
            {
                Count = 2,
                Done = 1,
                Files = new[] { "C:\\repo\\a.cs", "C:\\repo\\b.cs" },
            });

            var jobs = DeltaJobTracker.RunningJobs.ToList();
            Assert.HasCount(2, jobs);
            Assert.AreEqual(WebComponentConstants.StateTypes.RUNNING, jobs[0].State);
            Assert.AreEqual(WebComponentConstants.StateTypes.QUEUED, jobs[1].State);
            Assert.AreEqual(3, _notifier.TotalCount);
            Assert.AreEqual(2, _notifier.RemainingCount);
            Assert.AreEqual("running", _notifier.AnalysisState);
        }

        [TestMethod]
        public void ApplyQueue_ClearsTotalWhenNothingRemains()
        {
            _notifier.ApplyQueue(new ReviewQueue
            {
                Count = 2,
                Done = 1,
                Files = new[] { "a.cs" },
            });

            _notifier.ApplyQueue(new ReviewQueue
            {
                Count = 0,
                Done = 3,
                Files = Array.Empty<string>(),
            });

            Assert.IsEmpty(DeltaJobTracker.RunningJobs);
            Assert.IsNull(_notifier.TotalCount);
            Assert.AreEqual(0, _notifier.RemainingCount);
            Assert.AreEqual("idle", _notifier.AnalysisState);
        }

        [TestMethod]
        public void ApplyQueue_RequeueDuringBatch_KeepsTotalAtQueueSize()
        {
            ApplyCounts(19, 1);
            Assert.AreEqual(20, _notifier.TotalCount);

            ApplyCounts(23, 17);
            Assert.AreEqual(23, _notifier.TotalCount);

            ApplyCounts(23, 37);
            Assert.AreEqual(23, _notifier.TotalCount);
            Assert.AreEqual(23, _notifier.RemainingCount);
        }

        [TestMethod]
        public void ApplyQueue_QueueShrinksDuringBatch_LowersTotal()
        {
            ApplyCounts(448, 1);
            Assert.AreEqual(449, _notifier.TotalCount);

            ApplyCounts(27, 10);
            Assert.AreEqual(37, _notifier.TotalCount);
        }

        [TestMethod]
        public void ApplyQueue_NewBatchAfterDrain_StartsFromDoneAndRemaining()
        {
            ApplyCounts(19, 1);
            ApplyCounts(0, 20);

            ApplyCounts(3, 1);

            Assert.AreEqual(4, _notifier.TotalCount);
        }

        [TestMethod]
        public void SetAutomaticAnalysisEnabled_WhenDisabled_StopsAndClearsJobs()
        {
            _notifier.ApplyQueue(new ReviewQueue { Count = 1, Files = new[] { "a.cs" } });
            _notifier.SetAutomaticAnalysisEnabled(false);

            Assert.AreEqual("stopped", _notifier.AnalysisState);
            Assert.IsEmpty(DeltaJobTracker.RunningJobs);
        }

        [TestMethod]
        public void SetAutomaticAnalysisEnabled_WhenReenabledFromStopped_ReturnsToIdle()
        {
            _notifier.SetAutomaticAnalysisEnabled(false);
            var eventFired = false;
            _notifier.ViewUpdateRequested += (_, __) => eventFired = true;

            _notifier.SetAutomaticAnalysisEnabled(true);

            Assert.AreEqual("idle", _notifier.AnalysisState);
            Assert.IsTrue(eventFired);
        }

        [TestMethod]
        public void SetAutomaticAnalysisEnabled_NoOpWhenValueUnchanged()
        {
            _notifier.SetAutomaticAnalysisEnabled(true);
            _notifier.ApplyQueue(new ReviewQueue { Count = 1, Files = new[] { "a.cs" } });

            _notifier.SetAutomaticAnalysisEnabled(true);

            Assert.AreEqual("running", _notifier.AnalysisState);
            Assert.HasCount(1, DeltaJobTracker.RunningJobs);
        }

        [TestMethod]
        public void OnDeltaStarting_WhenAutomaticAnalysisDisabled_DoesNotTrackJob()
        {
            _notifier.SetAutomaticAnalysisEnabled(false);
            _notifier.OnDeltaStarting("file.cs");

            Assert.IsEmpty(DeltaJobTracker.RunningJobs);
        }

        [TestMethod]
        public void ApplyQueue_WhenAutomaticAnalysisDisabled_Stops()
        {
            _notifier.SetAutomaticAnalysisEnabled(false);

            _notifier.ApplyQueue(new ReviewQueue { Count = 2, Files = new[] { "a.cs", "b.cs" } });

            Assert.AreEqual("stopped", _notifier.AnalysisState);
            Assert.IsEmpty(DeltaJobTracker.RunningJobs);
        }

        [TestMethod]
        public void Clear_RemovesJobsAndRequestsViewUpdate()
        {
            var updates = 0;
            _notifier.ViewUpdateRequested += (sender, args) => updates++;
            _notifier.ApplyQueue(new ReviewQueue
            {
                Count = 1,
                Files = new[] { "C:\\repo\\a.cs" },
            });

            _notifier.Clear();

            Assert.IsEmpty(DeltaJobTracker.RunningJobs);
            Assert.IsGreaterThan(0, updates);
        }

        private void ApplyCounts(int count, int done)
        {
            _notifier.ApplyQueue(new ReviewQueue
            {
                Count = count,
                Done = done,
                Files = count > 0 ? new[] { "a.cs" } : Array.Empty<string>(),
            });
        }
    }
}
