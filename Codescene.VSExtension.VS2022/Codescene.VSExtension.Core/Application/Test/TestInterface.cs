using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Test
{
    public class TestInterface : ITestInterface
    {
        public int Test { get; set; }

        public TestEnum GetEnumValue()
        {
            return TestEnum.ExclamationMark;
        }
    }
}
