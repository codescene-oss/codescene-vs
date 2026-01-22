using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Test
{
    internal interface ITestInterface
    {
        public int Test { get; set; }
        public TestEnum GetEnumValue();
    }
}
