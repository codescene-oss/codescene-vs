// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CoreCountUtilsTests
    {
        [TestMethod]
        public void GetParallelizationCountByCoreCount_WithOneCore_ReturnsOne()
            => Assert.AreEqual(1, CoreCountUtils.GetParallelizationCountByCoreCount(1));

        [TestMethod]
        public void GetParallelizationCountByCoreCount_WithThreeCores_ReturnsOne()
            => Assert.AreEqual(1, CoreCountUtils.GetParallelizationCountByCoreCount(3));

        [TestMethod]
        public void GetParallelizationCountByCoreCount_WithFourCores_ReturnsOne()
            => Assert.AreEqual(1, CoreCountUtils.GetParallelizationCountByCoreCount(4));

        [TestMethod]
        public void GetParallelizationCountByCoreCount_WithSevenCores_ReturnsTwo()
            => Assert.AreEqual(2, CoreCountUtils.GetParallelizationCountByCoreCount(7));

        [TestMethod]
        public void GetParallelizationCountByCoreCount_WithTwelveCores_ReturnsThree()
            => Assert.AreEqual(3, CoreCountUtils.GetParallelizationCountByCoreCount(12));

        [TestMethod]
        public void GetParallelizationCountByCoreCount_WithZeroCores_ReturnsOne()
            => Assert.AreEqual(1, CoreCountUtils.GetParallelizationCountByCoreCount(0));
    }
}
