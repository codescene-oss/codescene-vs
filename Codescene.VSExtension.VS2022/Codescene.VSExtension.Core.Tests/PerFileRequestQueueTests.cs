// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class PerFileRequestQueueTests
    {
        [TestMethod]
        public void TryStart_FirstRequest_ReturnsTrue()
        {
            var queue = new PerFileRequestQueue<string>();

            var started = queue.TryStart("file.cs", "first");

            Assert.IsTrue(started);
        }

        [TestMethod]
        public void EnqueueLatest_ReplacesPendingRequest()
        {
            var queue = new PerFileRequestQueue<string>();
            queue.TryStart("file.cs", "first");

            queue.EnqueueLatest("file.cs", "second");
            queue.EnqueueLatest("file.cs", "third");

            var hasNext = queue.CompleteAndGetNext("file.cs", out var nextRequest);

            Assert.IsTrue(hasNext);
            Assert.AreEqual("third", nextRequest);
        }

        [TestMethod]
        public void CompleteAndGetNext_WithoutPendingRequest_ReturnsFalse()
        {
            var queue = new PerFileRequestQueue<string>();
            queue.TryStart("file.cs", "first");

            var hasNext = queue.CompleteAndGetNext("file.cs", out var nextRequest);

            Assert.IsFalse(hasNext);
            Assert.IsNull(nextRequest);
            Assert.IsTrue(queue.TryStart("file.cs", "second"));
        }

        [TestMethod]
        public void Clear_RemovesQueuedRequests()
        {
            var queue = new PerFileRequestQueue<string>();
            queue.TryStart("file.cs", "first");
            queue.EnqueueLatest("file.cs", "second");

            queue.Clear();

            var hasNext = queue.CompleteAndGetNext("file.cs", out var nextRequest);

            Assert.IsFalse(hasNext);
            Assert.IsNull(nextRequest);
            Assert.IsTrue(queue.TryStart("file.cs", "third"));
        }
    }
}
