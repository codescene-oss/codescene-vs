using Codescene.VSExtension.Core.Application.Test;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class TestInterfaceTests
    {
        [TestMethod]
        public void SampleTestMethod()
        {
            ITestInterface test = new TestInterface();
            var enumValue = test.GetEnumValue();

            Assert.AreEqual(TestEnum.ExclamationMark, enumValue);
        }
    }
}
