// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class WorkspaceActivityTrackerTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            WorkspaceActivityTracker.Reset();
        }

        [TestMethod]
        public void ConsumeActivity_WhenNotMarked_ReturnsFalse()
        {
            WorkspaceActivityTracker.Reset();

            Assert.IsFalse(WorkspaceActivityTracker.ConsumeActivity());
        }

        [TestMethod]
        public void MarkActivity_ThenConsume_ReturnsTrueOnce()
        {
            WorkspaceActivityTracker.Reset();
            WorkspaceActivityTracker.MarkActivity();

            Assert.IsTrue(WorkspaceActivityTracker.ConsumeActivity());
            Assert.IsFalse(WorkspaceActivityTracker.ConsumeActivity());
        }

        [TestMethod]
        public void Reset_ClearsPendingActivity()
        {
            WorkspaceActivityTracker.MarkActivity();
            WorkspaceActivityTracker.Reset();

            Assert.IsFalse(WorkspaceActivityTracker.ConsumeActivity());
        }
    }
}
