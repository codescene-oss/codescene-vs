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
    }
}
