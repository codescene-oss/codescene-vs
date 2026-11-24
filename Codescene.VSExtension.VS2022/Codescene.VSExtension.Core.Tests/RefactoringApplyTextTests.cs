// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Refactoring;
using Codescene.VSExtension.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class RefactoringApplyTextTests
    {
        [TestMethod]
        public void AgentShapedIndentedBody_InsertedVerbatim()
        {
            var code = "    public void RefactoredMethod()\r\n    {\r\n        return 42;\r\n    }";
            var detectIndentation = () => new IndentationInfo { Level = 2, UsesTabs = false, TabSize = 4 };

            var result = RefactoringApplyText.Prepare(code, detectIndentation);

            Assert.AreEqual(code, result);
        }

        [TestMethod]
        public void UnindentedCode_IsAdjusted()
        {
            var code = "public void RefactoredMethod()\r\n{\r\n    return 42;\r\n}";
            var detectIndentation = () => new IndentationInfo { Level = 2, UsesTabs = false, TabSize = 4 };

            var result = RefactoringApplyText.Prepare(code, detectIndentation);

            StringAssert.StartsWith(result, "        public void RefactoredMethod()");
        }

        [TestMethod]
        public void ZeroIndentation_NoAdjustment()
        {
            var code = "public void Method()\r\n{\r\n    return 42;\r\n}";
            var detectIndentation = () => new IndentationInfo { Level = 0, UsesTabs = false, TabSize = 4 };

            var result = RefactoringApplyText.Prepare(code, detectIndentation);

            Assert.AreEqual(code, result);
        }

        [TestMethod]
        public void EmptyCode_ReturnsEmpty()
        {
            var code = string.Empty;
            var detectIndentation = () => new IndentationInfo { Level = 2, UsesTabs = false, TabSize = 4 };

            var result = RefactoringApplyText.Prepare(code, detectIndentation);

            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void NullCode_ReturnsNull()
        {
            var detectIndentation = () => new IndentationInfo { Level = 2, UsesTabs = false, TabSize = 4 };

            var result = RefactoringApplyText.Prepare(null, detectIndentation);

            Assert.IsNull(result);
        }
    }
}
