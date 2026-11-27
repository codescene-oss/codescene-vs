// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class SimpleModelTests
    {
        [TestMethod]
        public void CodeSmellTooltipModel_PropertyAssignment_SetsAllProperties()
        {
            var range = new CodeRangeModel(1, 10, 0, 50);
            var functionRange = new CodeRangeModel(1, 20, 0, 0);

            var model = new CodeSmellTooltipModel
            {
                Category = "Complex Method",
                Details = "High cyclomatic complexity",
                Path = "src/test.cs",
                FunctionName = "ProcessData",
                Range = range,
                FunctionRange = functionRange,
            };

            Assert.AreEqual("Complex Method", model.Category);
            Assert.AreEqual("High cyclomatic complexity", model.Details);
            Assert.AreEqual("src/test.cs", model.Path);
            Assert.AreEqual("ProcessData", model.FunctionName);
            Assert.AreEqual(range, model.Range);
            Assert.AreEqual(functionRange, model.FunctionRange);
        }

        [TestMethod]
        public void CodeSmellTooltipModel_FunctionRange_DefaultsToNull()
        {
            var model = new CodeSmellTooltipModel();
            Assert.IsNull(model.FunctionRange);
        }

        [TestMethod]
        public void CustomDetailsData_PropertyAssignment_SetsAllProperties()
        {
            var model = new CustomDetailsData
            {
                FileName = "test.cs",
                Title = "Code Health Details",
            };

            Assert.AreEqual("test.cs", model.FileName);
            Assert.AreEqual("Code Health Details", model.Title);
        }

        [TestMethod]
        public void GitResult_Success_ReturnsTrueWhenExitCodeIsZero()
        {
            var result = new GitResult { ExitCode = 0, Output = "success", Error = string.Empty };
            Assert.IsTrue(result.Success);
        }

        [TestMethod]
        public void GitResult_Success_ReturnsFalseWhenExitCodeIsNonZero()
        {
            var result = new GitResult { ExitCode = 1, Output = string.Empty, Error = "error" };
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void GitResult_Success_ReturnsFalseWhenExitCodeIsNegative()
        {
            var result = new GitResult { ExitCode = -1, Output = string.Empty, Error = "fatal error" };
            Assert.IsFalse(result.Success);
        }

        [TestMethod]
        public void GitResult_PropertyAssignment_SetsAllProperties()
        {
            var result = new GitResult
            {
                ExitCode = 128,
                Output = "output text",
                Error = "error text",
            };

            Assert.AreEqual(128, result.ExitCode);
            Assert.AreEqual("output text", result.Output);
            Assert.AreEqual("error text", result.Error);
        }
    }
}
