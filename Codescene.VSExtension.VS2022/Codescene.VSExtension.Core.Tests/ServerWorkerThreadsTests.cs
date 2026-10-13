// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ServerWorkerThreadsTests
    {
        [TestMethod]
        public void Resolve_WhenConfiguredPositive_ReturnsConfiguredValue()
        {
            Assert.AreEqual(4, ServerWorkerThreads.Resolve(4));
        }

        [TestMethod]
        public void Resolve_WhenUnset_ReturnsAtLeastOneWorker()
        {
            Assert.IsGreaterThanOrEqualTo(1, ServerWorkerThreads.Resolve(0));
            Assert.IsGreaterThanOrEqualTo(1, ServerWorkerThreads.Resolve(-1));
        }
    }
}
