// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Services;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
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
    }
}
