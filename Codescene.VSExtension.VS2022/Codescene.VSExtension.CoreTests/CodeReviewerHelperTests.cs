using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class CodeReviewerHelperTests
    {
        private Mock<ILogger> _mockLogger;
        private AceRefactorableFunctionsCacheService _cacheService;

        private const string DefaultPath = "test/file.cs";
        private const string DefaultCode = "public void TestFunction() { }";
        private const string DefaultFunctionName = "TestFunction";

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _cacheService = new AceRefactorableFunctionsCacheService();
            _cacheService.Clear();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cacheService?.Clear();
        }

        #region Helper Methods

        private static FnToRefactorModel CreateRefactorableFunction(string name, int startLine, int endLine)
        {
            return new FnToRefactorModel
            {
                Name = name,
                Range = new CliRangeModel { Startline = startLine, EndLine = endLine }
            };
        }

        private static FunctionFindingModel CreateFunctionFinding(string name, int startLine, int endLine, FnToRefactorModel refactorableFn = null)
        {
            return new FunctionFindingModel
            {
                Function = new FunctionInfoModel
                {
                    Name = name,
                    Range = new CliRangeModel { Startline = startLine, EndLine = endLine }
                },
                RefactorableFn = refactorableFn
            };
        }

        private static FunctionFindingModel CreateFindingWithRange(int startLine, int endLine)
        {
            return new FunctionFindingModel
            {
                Function = new FunctionInfoModel
                {
                    Range = new CliRangeModel { Startline = startLine, EndLine = endLine }
                }
            };
        }

        private static DeltaResponseModel CreateDeltaWithFindings(params FunctionFindingModel[] findings)
        {
            return new DeltaResponseModel { FunctionLevelFindings = findings };
        }

        private void SetupCacheWithFunctions(string path, string code, params FnToRefactorModel[] functions)
        {
            var entry = new AceRefactorableFunctionsEntry(path, code, new List<FnToRefactorModel>(functions));
            _cacheService.Put(entry);
        }

        private void VerifyLogContains(string expectedContent)
        {
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains(expectedContent))), Times.Once);
        }

        #endregion

        #region ShouldSkipUpdate Tests

        [TestMethod]
        public void ShouldSkipUpdate_NullDelta_ReturnsTrue()
        {
            var refactorableFunctions = new List<FnToRefactorModel> { new FnToRefactorModel() };

            var result = CodeReviewerHelper.ShouldSkipUpdate(null, refactorableFunctions, _mockLogger.Object);

            Assert.IsTrue(result);
            VerifyLogContains("Delta response null");
        }

        [TestMethod]
        public void ShouldSkipUpdate_EmptyRefactorableFunctions_ReturnsTrue()
        {
            var result = CodeReviewerHelper.ShouldSkipUpdate(new DeltaResponseModel(), new List<FnToRefactorModel>(), _mockLogger.Object);

            Assert.IsTrue(result);
            VerifyLogContains("No refactorable functions found");
        }

        [TestMethod]
        public void ShouldSkipUpdate_ValidDeltaAndFunctions_ReturnsFalse()
        {
            var refactorableFunctions = new List<FnToRefactorModel> { new FnToRefactorModel() };

            var result = CodeReviewerHelper.ShouldSkipUpdate(new DeltaResponseModel(), refactorableFunctions, _mockLogger.Object);

            Assert.IsFalse(result);
        }

        #endregion

        #region CheckRange Tests

        [DataTestMethod]
        [DataRow(15, 25, 10, 30, true, DisplayName = "FunctionStartsInsideRefactorableRange")]
        [DataRow(10, 25, 10, 30, true, DisplayName = "FunctionStartsAtRefactorableStart")]
        [DataRow(30, 35, 10, 30, true, DisplayName = "FunctionStartsAtRefactorableEnd")]
        [DataRow(5, 25, 10, 30, false, DisplayName = "FunctionStartsBeforeRefactorableRange")]
        [DataRow(35, 40, 10, 30, false, DisplayName = "FunctionStartsAfterRefactorableRange")]
        public void CheckRange_VariousRanges_ReturnsExpectedResult(
            int findingStart, int findingEnd, int refStart, int refEnd, bool expectedResult)
        {
            var finding = CreateFindingWithRange(findingStart, findingEnd);
            var refFunction = CreateRefactorableFunction(null, refStart, refEnd);

            var result = CodeReviewerHelper.CheckRange(finding, refFunction);

            Assert.AreEqual(expectedResult, result);
        }

        #endregion

        #region UpdateFindingIfNotUpdated Tests

        [TestMethod]
        public void UpdateFindingIfNotUpdated_MatchingFunctionFound_SetsRefactorableFn()
        {
            var finding = CreateFunctionFinding(DefaultFunctionName, 15, 25);
            var refactorableFn = CreateRefactorableFunction(DefaultFunctionName, 10, 30);

            CodeReviewerHelper.UpdateFindingIfNotUpdated(finding, DefaultFunctionName, new List<FnToRefactorModel> { refactorableFn });

            Assert.AreEqual(refactorableFn, finding.RefactorableFn);
        }

        [TestMethod]
        public void UpdateFindingIfNotUpdated_NoMatchingFunction_RefactorableFnRemainsNull()
        {
            var finding = CreateFunctionFinding(DefaultFunctionName, 15, 25);
            var refactorableFn = CreateRefactorableFunction("DifferentFunction", 10, 30);

            CodeReviewerHelper.UpdateFindingIfNotUpdated(finding, DefaultFunctionName, new List<FnToRefactorModel> { refactorableFn });

            Assert.IsNull(finding.RefactorableFn);
        }

        [TestMethod]
        public void UpdateFindingIfNotUpdated_AlreadyHasRefactorableFn_DoesNotUpdate()
        {
            var existingRefactorableFn = new FnToRefactorModel { Name = "Existing" };
            var finding = CreateFunctionFinding(DefaultFunctionName, 15, 25, existingRefactorableFn);
            var newRefactorableFn = CreateRefactorableFunction(DefaultFunctionName, 10, 30);

            CodeReviewerHelper.UpdateFindingIfNotUpdated(finding, DefaultFunctionName, new List<FnToRefactorModel> { newRefactorableFn });

            Assert.AreEqual(existingRefactorableFn, finding.RefactorableFn);
        }

        [TestMethod]
        public void UpdateFindingIfNotUpdated_MatchingNameButOutOfRange_DoesNotUpdate()
        {
            var finding = CreateFunctionFinding(DefaultFunctionName, 50, 60);
            var refactorableFn = CreateRefactorableFunction(DefaultFunctionName, 10, 30);

            CodeReviewerHelper.UpdateFindingIfNotUpdated(finding, DefaultFunctionName, new List<FnToRefactorModel> { refactorableFn });

            Assert.IsNull(finding.RefactorableFn);
        }

        #endregion

        #region UpdateFindings Tests

        [TestMethod]
        public void UpdateFindings_MultipleFindingsWithMatches_UpdatesAll()
        {
            var delta = CreateDeltaWithFindings(
                CreateFunctionFinding("Function1", 10, 20),
                CreateFunctionFinding("Function2", 30, 40)
            );
            var refactorableFunctions = new List<FnToRefactorModel>
            {
                CreateRefactorableFunction("Function1", 5, 25),
                CreateRefactorableFunction("Function2", 25, 45)
            };

            CodeReviewerHelper.UpdateFindings(delta, refactorableFunctions);

            Assert.IsNotNull(delta.FunctionLevelFindings[0].RefactorableFn);
            Assert.IsNotNull(delta.FunctionLevelFindings[1].RefactorableFn);
        }

        [DataTestMethod]
        [DataRow(null, DisplayName = "NullName")]
        [DataRow("", DisplayName = "EmptyName")]
        public void UpdateFindings_InvalidFunctionName_Skipped(string functionName)
        {
            var delta = CreateDeltaWithFindings(CreateFunctionFinding(functionName, 10, 20));
            var refactorableFunctions = new List<FnToRefactorModel>
            {
                CreateRefactorableFunction("SomeFunction", 5, 25)
            };

            CodeReviewerHelper.UpdateFindings(delta, refactorableFunctions);

            Assert.IsNull(delta.FunctionLevelFindings[0].RefactorableFn);
        }

        [TestMethod]
        public void UpdateFindings_NullFunctionInfo_Skipped()
        {
            var delta = CreateDeltaWithFindings(new FunctionFindingModel { Function = null });
            var refactorableFunctions = new List<FnToRefactorModel>
            {
                CreateRefactorableFunction("SomeFunction", 5, 25)
            };

            CodeReviewerHelper.UpdateFindings(delta, refactorableFunctions);

            Assert.IsNull(delta.FunctionLevelFindings[0].RefactorableFn);
        }

        #endregion

        #region UpdateDeltaCacheWithRefactorableFunctions Tests

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithMatchingCacheEntry_UpdatesDeltaFindings()
        {
            SetupCacheWithFunctions(DefaultPath, DefaultCode, CreateRefactorableFunction(DefaultFunctionName, 1, 10));
            var delta = CreateDeltaWithFindings(CreateFunctionFinding(DefaultFunctionName, 1, 10));

            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, DefaultPath, DefaultCode, _mockLogger.Object);

            Assert.IsNotNull(delta.FunctionLevelFindings[0].RefactorableFn);
            Assert.AreEqual(DefaultFunctionName, delta.FunctionLevelFindings[0].RefactorableFn.Name);
            VerifyLogContains("Found 1 refactorable functions");
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithEmptyCache_DoesNotUpdateDelta()
        {
            var delta = CreateDeltaWithFindings(CreateFunctionFinding(DefaultFunctionName, 1, 10));

            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, DefaultPath, DefaultCode, _mockLogger.Object);

            Assert.IsNull(delta.FunctionLevelFindings[0].RefactorableFn);
            VerifyLogContains("No refactorable functions found");
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithNullDelta_SkipsUpdate()
        {
            SetupCacheWithFunctions(DefaultPath, DefaultCode, CreateRefactorableFunction(DefaultFunctionName, 1, 10));

            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(null, DefaultPath, DefaultCode, _mockLogger.Object);

            VerifyLogContains("Delta response null");
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithMismatchedCode_DoesNotUpdateDelta()
        {
            SetupCacheWithFunctions(DefaultPath, DefaultCode, CreateRefactorableFunction(DefaultFunctionName, 1, 10));
            var delta = CreateDeltaWithFindings(CreateFunctionFinding(DefaultFunctionName, 1, 10));

            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, DefaultPath, "different code", _mockLogger.Object);

            Assert.IsNull(delta.FunctionLevelFindings[0].RefactorableFn);
            VerifyLogContains("No refactorable functions found");
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithMultipleFunctions_UpdatesMatchingFindings()
        {
            var code = "public void Func1() { } public void Func2() { }";
            SetupCacheWithFunctions(DefaultPath, code,
                CreateRefactorableFunction("Func1", 1, 10),
                CreateRefactorableFunction("Func2", 12, 20),
                CreateRefactorableFunction("Func3", 22, 30)
            );
            var delta = CreateDeltaWithFindings(
                CreateFunctionFinding("Func1", 5, 8),
                CreateFunctionFinding("Func2", 15, 18),
                CreateFunctionFinding("NonExistentFunc", 50, 60)
            );

            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, DefaultPath, code, _mockLogger.Object);

            Assert.AreEqual("Func1", delta.FunctionLevelFindings[0].RefactorableFn?.Name);
            Assert.AreEqual("Func2", delta.FunctionLevelFindings[1].RefactorableFn?.Name);
            Assert.IsNull(delta.FunctionLevelFindings[2].RefactorableFn);
            VerifyLogContains("Found 3 refactorable functions");
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithEmptyFunctionLevelFindings_DoesNotThrow()
        {
            SetupCacheWithFunctions(DefaultPath, DefaultCode, CreateRefactorableFunction(DefaultFunctionName, 1, 10));
            var delta = CreateDeltaWithFindings();

            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, DefaultPath, DefaultCode, _mockLogger.Object);
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_LogsCorrectPath()
        {
            var path = "specific/test/path.cs";
            var delta = CreateDeltaWithFindings();

            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, path, "code", _mockLogger.Object);

            VerifyLogContains(path);
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithFunctionOutOfRange_DoesNotUpdate()
        {
            SetupCacheWithFunctions(DefaultPath, DefaultCode, CreateRefactorableFunction(DefaultFunctionName, 100, 110));
            var delta = CreateDeltaWithFindings(CreateFunctionFinding(DefaultFunctionName, 1, 10));

            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, DefaultPath, DefaultCode, _mockLogger.Object);

            Assert.IsNull(delta.FunctionLevelFindings[0].RefactorableFn);
        }

        #endregion
    }
}
