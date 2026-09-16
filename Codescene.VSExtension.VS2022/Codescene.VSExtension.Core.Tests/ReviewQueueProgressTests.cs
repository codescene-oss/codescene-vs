// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Services;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ReviewQueueProgressTests
    {
        [TestMethod]
        public void Update_ZeroCount_FlushesImmediately()
        {
            var snapshots = new List<ReviewQueue>();
            using (var progress = new ReviewQueueProgress(snapshots.Add))
            {
                progress.Update(new ReviewQueue { Count = 1, Files = new[] { "a.cs" } });
                progress.Update(new ReviewQueue { Count = 0, Files = Array.Empty<string>() });
            }

            Assert.IsTrue(snapshots.Exists(queue => queue.Count == 0));
        }

        [TestMethod]
        public void Update_SubsequentPositiveCount_FlushesLatestSnapshot()
        {
            var flushed = new ManualResetEventSlim();
            var snapshots = new List<ReviewQueue>();
            using (var progress = new ReviewQueueProgress(snapshot =>
            {
                snapshots.Add(snapshot);
                flushed.Set();
            }))
            {
                progress.Update(new ReviewQueue { Count = 1, Files = new[] { "a.cs" } });
                Assert.IsTrue(flushed.Wait(TimeSpan.FromSeconds(5)));
                flushed.Reset();

                progress.Update(new ReviewQueue { Count = 2, Files = new[] { "a.cs", "b.cs" } });

                Assert.IsTrue(flushed.Wait(TimeSpan.FromSeconds(5)));
            }

            Assert.AreEqual(2, snapshots[snapshots.Count - 1].Count);
        }

        [TestMethod]
        public void Update_AfterDispose_IsIgnored()
        {
            var changes = 0;
            var progress = new ReviewQueueProgress(_ => changes++);
            progress.Dispose();

            progress.Update(new ReviewQueue { Count = 1 });

            Assert.AreEqual(0, changes);
        }

        [TestMethod]
        public void ApplyQueue_MapsRunningAndQueuedJobs()
        {
            DeltaJobTracker.Clear();
            var notifier = new CodeHealthMonitorNotifier();
            notifier.ApplyQueue(new ReviewQueue
            {
                Count = 2,
                Files = new[] { "C:\\repo\\a.cs", "C:\\repo\\b.cs" },
            });

            var jobs = DeltaJobTracker.RunningJobs.ToList();
            Assert.HasCount(2, jobs);
            Assert.AreEqual(WebComponentConstants.StateTypes.RUNNING, jobs[0].State);
            Assert.AreEqual(WebComponentConstants.StateTypes.QUEUED, jobs[1].State);
            DeltaJobTracker.Clear();
        }

        [TestMethod]
        public void Clear_RemovesJobsAndRequestsViewUpdate()
        {
            DeltaJobTracker.Clear();
            var notifier = new CodeHealthMonitorNotifier();
            var updates = 0;
            notifier.ViewUpdateRequested += (sender, args) => updates++;
            notifier.ApplyQueue(new ReviewQueue
            {
                Count = 1,
                Files = new[] { "C:\\repo\\a.cs" },
            });

            notifier.Clear();

            Assert.IsEmpty(DeltaJobTracker.RunningJobs);
            Assert.IsGreaterThan(0, updates);
        }
    }
}
