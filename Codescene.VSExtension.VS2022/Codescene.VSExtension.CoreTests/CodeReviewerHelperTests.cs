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

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
        }

        #region ShouldSkipUpdate Tests

        [TestMethod]
        public void ShouldSkipUpdate_NullDelta_ReturnsTrue()
        {
            // Arrange
            DeltaResponseModel delta = null;
            var refactorableFunctions = new List<FnToRefactorModel> { new FnToRefactorModel() };

            // Act
            var result = CodeReviewerHelper.ShouldSkipUpdate(delta, refactorableFunctions, _mockLogger.Object);

            // Assert
            Assert.IsTrue(result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Delta response null"))), Times.Once);
        }

        [TestMethod]
        public void ShouldSkipUpdate_EmptyRefactorableFunctions_ReturnsTrue()
        {
            // Arrange
            var delta = new DeltaResponseModel();
            var refactorableFunctions = new List<FnToRefactorModel>();

            // Act
            var result = CodeReviewerHelper.ShouldSkipUpdate(delta, refactorableFunctions, _mockLogger.Object);

            // Assert
            Assert.IsTrue(result);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("No refactorable functions found"))), Times.Once);
        }

        [TestMethod]
        public void ShouldSkipUpdate_ValidDeltaAndFunctions_ReturnsFalse()
        {
            // Arrange
            var delta = new DeltaResponseModel();
            var refactorableFunctions = new List<FnToRefactorModel> { new FnToRefactorModel() };

            // Act
            var result = CodeReviewerHelper.ShouldSkipUpdate(delta, refactorableFunctions, _mockLogger.Object);

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region CheckRange Tests

        [TestMethod]
        public void CheckRange_FunctionStartsInsideRefactorableRange_ReturnsTrue()
        {
            // Arrange
            var finding = new FunctionFindingModel
            {
                Function = new FunctionInfoModel
                {
                    Range = new CliRangeModel { Startline = 15, EndLine = 25 }
                }
            };
            var refFunction = new FnToRefactorModel
            {
                Range = new CliRangeModel { Startline = 10, EndLine = 30 }
            };

            // Act
            var result = CodeReviewerHelper.CheckRange(finding, refFunction);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CheckRange_FunctionStartsAtRefactorableStart_ReturnsTrue()
        {
            // Arrange
            var finding = new FunctionFindingModel
            {
                Function = new FunctionInfoModel
                {
                    Range = new CliRangeModel { Startline = 10, EndLine = 25 }
                }
            };
            var refFunction = new FnToRefactorModel
            {
                Range = new CliRangeModel { Startline = 10, EndLine = 30 }
            };

            // Act
            var result = CodeReviewerHelper.CheckRange(finding, refFunction);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CheckRange_FunctionStartsAtRefactorableEnd_ReturnsTrue()
        {
            // Arrange
            var finding = new FunctionFindingModel
            {
                Function = new FunctionInfoModel
                {
                    Range = new CliRangeModel { Startline = 30, EndLine = 35 }
                }
            };
            var refFunction = new FnToRefactorModel
            {
                Range = new CliRangeModel { Startline = 10, EndLine = 30 }
            };

            // Act
            var result = CodeReviewerHelper.CheckRange(finding, refFunction);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void CheckRange_FunctionStartsBeforeRefactorableRange_ReturnsFalse()
        {
            // Arrange
            var finding = new FunctionFindingModel
            {
                Function = new FunctionInfoModel
                {
                    Range = new CliRangeModel { Startline = 5, EndLine = 25 }
                }
            };
            var refFunction = new FnToRefactorModel
            {
                Range = new CliRangeModel { Startline = 10, EndLine = 30 }
            };

            // Act
            var result = CodeReviewerHelper.CheckRange(finding, refFunction);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void CheckRange_FunctionStartsAfterRefactorableRange_ReturnsFalse()
        {
            // Arrange
            var finding = new FunctionFindingModel
            {
                Function = new FunctionInfoModel
                {
                    Range = new CliRangeModel { Startline = 35, EndLine = 40 }
                }
            };
            var refFunction = new FnToRefactorModel
            {
                Range = new CliRangeModel { Startline = 10, EndLine = 30 }
            };

            // Act
            var result = CodeReviewerHelper.CheckRange(finding, refFunction);

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region UpdateFindingIfNotUpdated Tests

        [TestMethod]
        public void UpdateFindingIfNotUpdated_MatchingFunctionFound_SetsRefactorableFn()
        {
            // Arrange
            var finding = new FunctionFindingModel
            {
                Function = new FunctionInfoModel
                {
                    Name = "TestFunction",
                    Range = new CliRangeModel { Startline = 15, EndLine = 25 }
                },
                RefactorableFn = null
            };
            var refactorableFn = new FnToRefactorModel
            {
                Name = "TestFunction",
                Range = new CliRangeModel { Startline = 10, EndLine = 30 }
            };
            var refactorableFunctions = new List<FnToRefactorModel> { refactorableFn };

            // Act
            CodeReviewerHelper.UpdateFindingIfNotUpdated(finding, "TestFunction", refactorableFunctions);

            // Assert
            Assert.IsNotNull(finding.RefactorableFn);
            Assert.AreEqual(refactorableFn, finding.RefactorableFn);
        }

        [TestMethod]
        public void UpdateFindingIfNotUpdated_NoMatchingFunction_RefactorableFnRemainsNull()
        {
            // Arrange
            var finding = new FunctionFindingModel
            {
                Function = new FunctionInfoModel
                {
                    Name = "TestFunction",
                    Range = new CliRangeModel { Startline = 15, EndLine = 25 }
                },
                RefactorableFn = null
            };
            var refactorableFn = new FnToRefactorModel
            {
                Name = "DifferentFunction",
                Range = new CliRangeModel { Startline = 10, EndLine = 30 }
            };
            var refactorableFunctions = new List<FnToRefactorModel> { refactorableFn };

            // Act
            CodeReviewerHelper.UpdateFindingIfNotUpdated(finding, "TestFunction", refactorableFunctions);

            // Assert
            Assert.IsNull(finding.RefactorableFn);
        }

        [TestMethod]
        public void UpdateFindingIfNotUpdated_AlreadyHasRefactorableFn_DoesNotUpdate()
        {
            // Arrange
            var existingRefactorableFn = new FnToRefactorModel { Name = "Existing" };
            var finding = new FunctionFindingModel
            {
                Function = new FunctionInfoModel
                {
                    Name = "TestFunction",
                    Range = new CliRangeModel { Startline = 15, EndLine = 25 }
                },
                RefactorableFn = existingRefactorableFn
            };
            var newRefactorableFn = new FnToRefactorModel
            {
                Name = "TestFunction",
                Range = new CliRangeModel { Startline = 10, EndLine = 30 }
            };
            var refactorableFunctions = new List<FnToRefactorModel> { newRefactorableFn };

            // Act
            CodeReviewerHelper.UpdateFindingIfNotUpdated(finding, "TestFunction", refactorableFunctions);

            // Assert
            Assert.AreEqual(existingRefactorableFn, finding.RefactorableFn);
        }

        [TestMethod]
        public void UpdateFindingIfNotUpdated_MatchingNameButOutOfRange_DoesNotUpdate()
        {
            // Arrange
            var finding = new FunctionFindingModel
            {
                Function = new FunctionInfoModel
                {
                    Name = "TestFunction",
                    Range = new CliRangeModel { Startline = 50, EndLine = 60 }
                },
                RefactorableFn = null
            };
            var refactorableFn = new FnToRefactorModel
            {
                Name = "TestFunction",
                Range = new CliRangeModel { Startline = 10, EndLine = 30 }
            };
            var refactorableFunctions = new List<FnToRefactorModel> { refactorableFn };

            // Act
            CodeReviewerHelper.UpdateFindingIfNotUpdated(finding, "TestFunction", refactorableFunctions);

            // Assert
            Assert.IsNull(finding.RefactorableFn);
        }

        #endregion

        #region UpdateFindings Tests

        [TestMethod]
        public void UpdateFindings_MultipleFindingsWithMatches_UpdatesAll()
        {
            // Arrange
            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "Function1",
                            Range = new CliRangeModel { Startline = 10, EndLine = 20 }
                        }
                    },
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "Function2",
                            Range = new CliRangeModel { Startline = 30, EndLine = 40 }
                        }
                    }
                }
            };
            var refactorableFunctions = new List<FnToRefactorModel>
            {
                new FnToRefactorModel { Name = "Function1", Range = new CliRangeModel { Startline = 5, EndLine = 25 } },
                new FnToRefactorModel { Name = "Function2", Range = new CliRangeModel { Startline = 25, EndLine = 45 } }
            };

            // Act
            CodeReviewerHelper.UpdateFindings(delta, refactorableFunctions);

            // Assert
            Assert.IsNotNull(delta.FunctionLevelFindings[0].RefactorableFn);
            Assert.IsNotNull(delta.FunctionLevelFindings[1].RefactorableFn);
        }

        [TestMethod]
        public void UpdateFindings_FunctionWithNullName_Skipped()
        {
            // Arrange
            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = null,
                            Range = new CliRangeModel { Startline = 10, EndLine = 20 }
                        }
                    }
                }
            };
            var refactorableFunctions = new List<FnToRefactorModel>
            {
                new FnToRefactorModel { Name = "SomeFunction", Range = new CliRangeModel { Startline = 5, EndLine = 25 } }
            };

            // Act
            CodeReviewerHelper.UpdateFindings(delta, refactorableFunctions);

            // Assert
            Assert.IsNull(delta.FunctionLevelFindings[0].RefactorableFn);
        }

        [TestMethod]
        public void UpdateFindings_FunctionWithEmptyName_Skipped()
        {
            // Arrange
            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "",
                            Range = new CliRangeModel { Startline = 10, EndLine = 20 }
                        }
                    }
                }
            };
            var refactorableFunctions = new List<FnToRefactorModel>
            {
                new FnToRefactorModel { Name = "SomeFunction", Range = new CliRangeModel { Startline = 5, EndLine = 25 } }
            };

            // Act
            CodeReviewerHelper.UpdateFindings(delta, refactorableFunctions);

            // Assert
            Assert.IsNull(delta.FunctionLevelFindings[0].RefactorableFn);
        }

        [TestMethod]
        public void UpdateFindings_NullFunctionInfo_Skipped()
        {
            // Arrange
            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = null
                    }
                }
            };
            var refactorableFunctions = new List<FnToRefactorModel>
            {
                new FnToRefactorModel { Name = "SomeFunction", Range = new CliRangeModel { Startline = 5, EndLine = 25 } }
            };

            // Act
            CodeReviewerHelper.UpdateFindings(delta, refactorableFunctions);

            // Assert
            Assert.IsNull(delta.FunctionLevelFindings[0].RefactorableFn);
        }

        #endregion

        #region UpdateDeltaCacheWithRefactorableFunctions Tests

        private AceRefactorableFunctionsCacheService _cacheService;

        [TestCleanup]
        public void Cleanup()
        {
            // Clear the static cache after each test to avoid test pollution
            _cacheService?.Clear();
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithMatchingCacheEntry_UpdatesDeltaFindings()
        {
            // Arrange
            _cacheService = new AceRefactorableFunctionsCacheService();
            var path = "test/file.cs";
            var code = "public void TestFunction() { }";
            var refactorableFn = new FnToRefactorModel
            {
                Name = "TestFunction",
                Range = new CliRangeModel { Startline = 1, EndLine = 10 }
            };
            var entry = new AceRefactorableFunctionsEntry(path, code, new List<FnToRefactorModel> { refactorableFn });
            _cacheService.Put(entry);

            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "TestFunction",
                            Range = new CliRangeModel { Startline = 1, EndLine = 10 }
                        }
                    }
                }
            };

            // Act
            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, path, code, _mockLogger.Object);

            // Assert
            Assert.IsNotNull(delta.FunctionLevelFindings[0].RefactorableFn);
            Assert.AreEqual("TestFunction", delta.FunctionLevelFindings[0].RefactorableFn.Name);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Found 1 refactorable functions"))), Times.Once);
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithEmptyCache_DoesNotUpdateDelta()
        {
            // Arrange
            _cacheService = new AceRefactorableFunctionsCacheService();
            _cacheService.Clear(); // Ensure cache is empty
            var path = "test/file.cs";
            var code = "public void TestFunction() { }";
            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "TestFunction",
                            Range = new CliRangeModel { Startline = 1, EndLine = 10 }
                        }
                    }
                }
            };

            // Act
            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, path, code, _mockLogger.Object);

            // Assert
            Assert.IsNull(delta.FunctionLevelFindings[0].RefactorableFn);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("No refactorable functions found"))), Times.Once);
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithNullDelta_SkipsUpdate()
        {
            // Arrange
            _cacheService = new AceRefactorableFunctionsCacheService();
            var path = "test/file.cs";
            var code = "public void TestFunction() { }";
            var refactorableFn = new FnToRefactorModel
            {
                Name = "TestFunction",
                Range = new CliRangeModel { Startline = 1, EndLine = 10 }
            };
            var entry = new AceRefactorableFunctionsEntry(path, code, new List<FnToRefactorModel> { refactorableFn });
            _cacheService.Put(entry);

            DeltaResponseModel delta = null;

            // Act
            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, path, code, _mockLogger.Object);

            // Assert
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Delta response null"))), Times.Once);
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithMismatchedCode_DoesNotUpdateDelta()
        {
            // Arrange
            _cacheService = new AceRefactorableFunctionsCacheService();
            var path = "test/file.cs";
            var originalCode = "public void TestFunction() { }";
            var differentCode = "public void DifferentFunction() { }";
            var refactorableFn = new FnToRefactorModel
            {
                Name = "TestFunction",
                Range = new CliRangeModel { Startline = 1, EndLine = 10 }
            };
            var entry = new AceRefactorableFunctionsEntry(path, originalCode, new List<FnToRefactorModel> { refactorableFn });
            _cacheService.Put(entry);

            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "TestFunction",
                            Range = new CliRangeModel { Startline = 1, EndLine = 10 }
                        }
                    }
                }
            };

            // Act - Use different code which won't match the cache hash
            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, path, differentCode, _mockLogger.Object);

            // Assert - Cache returns empty list when hash doesn't match
            Assert.IsNull(delta.FunctionLevelFindings[0].RefactorableFn);
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("No refactorable functions found"))), Times.Once);
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithMultipleFunctions_UpdatesMatchingFindings()
        {
            // Arrange
            _cacheService = new AceRefactorableFunctionsCacheService();
            var path = "test/file.cs";
            var code = "public void Func1() { } public void Func2() { }";
            var refactorableFunctions = new List<FnToRefactorModel>
            {
                new FnToRefactorModel { Name = "Func1", Range = new CliRangeModel { Startline = 1, EndLine = 10 } },
                new FnToRefactorModel { Name = "Func2", Range = new CliRangeModel { Startline = 12, EndLine = 20 } },
                new FnToRefactorModel { Name = "Func3", Range = new CliRangeModel { Startline = 22, EndLine = 30 } }
            };
            var entry = new AceRefactorableFunctionsEntry(path, code, refactorableFunctions);
            _cacheService.Put(entry);

            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "Func1",
                            Range = new CliRangeModel { Startline = 5, EndLine = 8 }
                        }
                    },
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "Func2",
                            Range = new CliRangeModel { Startline = 15, EndLine = 18 }
                        }
                    },
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "NonExistentFunc",
                            Range = new CliRangeModel { Startline = 50, EndLine = 60 }
                        }
                    }
                }
            };

            // Act
            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, path, code, _mockLogger.Object);

            // Assert
            Assert.IsNotNull(delta.FunctionLevelFindings[0].RefactorableFn);
            Assert.AreEqual("Func1", delta.FunctionLevelFindings[0].RefactorableFn.Name);
            Assert.IsNotNull(delta.FunctionLevelFindings[1].RefactorableFn);
            Assert.AreEqual("Func2", delta.FunctionLevelFindings[1].RefactorableFn.Name);
            Assert.IsNull(delta.FunctionLevelFindings[2].RefactorableFn); // Non-matching function
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("Found 3 refactorable functions"))), Times.Once);
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithEmptyFunctionLevelFindings_DoesNotThrow()
        {
            // Arrange
            _cacheService = new AceRefactorableFunctionsCacheService();
            var path = "test/file.cs";
            var code = "public void TestFunction() { }";
            var refactorableFn = new FnToRefactorModel
            {
                Name = "TestFunction",
                Range = new CliRangeModel { Startline = 1, EndLine = 10 }
            };
            var entry = new AceRefactorableFunctionsEntry(path, code, new List<FnToRefactorModel> { refactorableFn });
            _cacheService.Put(entry);

            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new FunctionFindingModel[0]
            };

            // Act & Assert - Should not throw
            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, path, code, _mockLogger.Object);
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_LogsCorrectPath()
        {
            // Arrange
            _cacheService = new AceRefactorableFunctionsCacheService();
            var path = "specific/test/path.cs";
            var code = "code content";

            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new FunctionFindingModel[0]
            };

            // Act
            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, path, code, _mockLogger.Object);

            // Assert
            _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains(path))), Times.Once);
        }

        [TestMethod]
        public void UpdateDeltaCacheWithRefactorableFunctions_WithFunctionOutOfRange_DoesNotUpdate()
        {
            // Arrange
            _cacheService = new AceRefactorableFunctionsCacheService();
            var path = "test/file.cs";
            var code = "public void TestFunction() { }";
            var refactorableFn = new FnToRefactorModel
            {
                Name = "TestFunction",
                Range = new CliRangeModel { Startline = 100, EndLine = 110 }
            };
            var entry = new AceRefactorableFunctionsEntry(path, code, new List<FnToRefactorModel> { refactorableFn });
            _cacheService.Put(entry);

            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "TestFunction",
                            Range = new CliRangeModel { Startline = 1, EndLine = 10 }
                        }
                    }
                }
            };

            // Act
            CodeReviewerHelper.UpdateDeltaCacheWithRefactorableFunctions(delta, path, code, _mockLogger.Object);

            // Assert - Name matches but range doesn't
            Assert.IsNull(delta.FunctionLevelFindings[0].RefactorableFn);
        }

        #endregion
    }
}
