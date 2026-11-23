// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CachedFindingLookupTests
    {
        [TestMethod]
        public void FindByFunctionName_ReturnsMatchingFinding()
        {
            var refactorableFn = new FnToRefactorModel { Name = "TestFunction" };
            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel { Name = "OtherFunction" },
                        RefactorableFn = new FnToRefactorModel { Name = "OtherFunction" },
                    },
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel { Name = "TestFunction" },
                        RefactorableFn = refactorableFn,
                    },
                },
            };

            var result = CachedFindingLookup.FindByFunctionName(delta, "TestFunction");

            Assert.IsNotNull(result);
            Assert.AreSame(refactorableFn, result);
        }

        [TestMethod]
        public void NoMatch_ReturnsNull()
        {
            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel { Name = "OtherFunction" },
                        RefactorableFn = new FnToRefactorModel { Name = "OtherFunction" },
                    },
                },
            };

            var result = CachedFindingLookup.FindByFunctionName(delta, "TestFunction");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void NullDelta_ReturnsNull()
        {
            var result = CachedFindingLookup.FindByFunctionName(null, "TestFunction");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void EmptyFunctionName_ReturnsNull()
        {
            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel { Name = "TestFunction" },
                        RefactorableFn = new FnToRefactorModel { Name = "TestFunction" },
                    },
                },
            };

            var result = CachedFindingLookup.FindByFunctionName(delta, string.Empty);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void NullFunctionName_ReturnsNull()
        {
            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel { Name = "TestFunction" },
                        RefactorableFn = new FnToRefactorModel { Name = "TestFunction" },
                    },
                },
            };

            var result = CachedFindingLookup.FindByFunctionName(delta, null);

            Assert.IsNull(result);
        }
    }
}
