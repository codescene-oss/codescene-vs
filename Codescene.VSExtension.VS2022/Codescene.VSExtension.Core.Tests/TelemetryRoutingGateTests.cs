// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Telemetry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class TelemetryRoutingGateTests
    {
        private List<string> _sent;

        [TestInitialize]
        public void Setup()
        {
            _sent = new List<string>();
        }

        [TestMethod]
        public void TryEnqueue_WhenNotHeld_ReturnsFalse()
        {
            var gate = CreateGate();

            Assert.IsFalse(gate.TryEnqueue("a"));
            Assert.IsEmpty(_sent);
        }

        [TestMethod]
        public void Ready_ForCurrentGeneration_ReleasesInOrder()
        {
            var gate = CreateGate();
            var generation = gate.Hold();

            Assert.IsTrue(gate.TryEnqueue("a"));
            Assert.IsTrue(gate.TryEnqueue("b"));
            gate.Ready(generation);

            CollectionAssert.AreEqual(new[] { "a", "b" }, _sent);
            Assert.IsFalse(gate.IsHeld);
        }

        [TestMethod]
        public void Ready_ForStaleGeneration_KeepsHolding()
        {
            var gate = CreateGate();
            var first = gate.Hold();
            var second = gate.Hold();

            gate.TryEnqueue("a");
            gate.Ready(first);

            Assert.IsEmpty(_sent);
            Assert.IsTrue(gate.IsHeld);

            gate.Ready(second);
            CollectionAssert.AreEqual(new[] { "a" }, _sent);
        }

        [TestMethod]
        public void Release_MovesFirstItemToFront()
        {
            var gate = CreateGate(isFirst: item => item == "session/started");
            var generation = gate.Hold();

            gate.TryEnqueue("auth/sign-out");
            gate.TryEnqueue("session/started");
            gate.TryEnqueue("docs/open-code-health");
            gate.Ready(generation);

            CollectionAssert.AreEqual(new[] { "session/started", "auth/sign-out", "docs/open-code-health" }, _sent);
        }

        [TestMethod]
        public void TryEnqueue_BeyondMaxHeld_DropsOldest()
        {
            var gate = CreateGate(maxHeld: 2);
            var generation = gate.Hold();

            gate.TryEnqueue("a");
            gate.TryEnqueue("b");
            gate.TryEnqueue("c");
            gate.Ready(generation);

            CollectionAssert.AreEqual(new[] { "b", "c" }, _sent);
        }

        [TestMethod]
        public async Task Bound_WhenNeverReady_ReleasesHeldItems()
        {
            var gate = CreateGate(bound: TimeSpan.FromMilliseconds(20));
            gate.Hold();
            gate.TryEnqueue("a");

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (_sent.Count == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);
            }

            CollectionAssert.AreEqual(new[] { "a" }, _sent);
            Assert.IsFalse(gate.IsHeld);
        }

        [TestMethod]
        public void Close_ReleasesHeldItemsAndIgnoresLaterHolds()
        {
            var gate = CreateGate();
            gate.Hold();
            gate.TryEnqueue("a");

            gate.Close();
            gate.Hold();

            CollectionAssert.AreEqual(new[] { "a" }, _sent);
            Assert.IsFalse(gate.IsHeld);
            Assert.IsFalse(gate.TryEnqueue("b"));
        }

        private TelemetryRoutingGate<string> CreateGate(Func<string, bool> isFirst = null, TimeSpan? bound = null, int maxHeld = 100)
        {
            return new TelemetryRoutingGate<string>(item => _sent.Add(item), isFirst, bound ?? TimeSpan.FromMinutes(5), maxHeld);
        }
    }
}
