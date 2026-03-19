// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Services;
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
    }
}
