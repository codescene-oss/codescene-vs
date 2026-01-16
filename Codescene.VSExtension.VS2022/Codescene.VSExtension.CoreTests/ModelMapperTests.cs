using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class ModelMapperTests
    {
        private readonly ModelMapper _mapper;

        public ModelMapperTests()
        {
            _mapper = new ModelMapper();
        }

        [TestMethod]
        public void Map_NullCliReviewModel_ReturnsEmptyFileReviewModel()
        {
            // Arrange
            var filePath = "test.cs";

            // Act
            var result = _mapper.Map(filePath, null);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(filePath, result.FilePath);
            Assert.AreEqual(0, result.Score);
            Assert.IsNull(result.RawScore);
            Assert.AreEqual(0, result.FileLevel.Count);
            Assert.AreEqual(0, result.FunctionLevel.Count);
        }

        [TestMethod]
        public void Map_CliReviewModelWithScore_MapsScoreCorrectly()
        {
            // Arrange
            var filePath = "test.cs";
            var cliReview = new CliReviewModel
            {
                Score = 8.5f,
                RawScore = "base64encodeddata"
            };

            // Act
            var result = _mapper.Map(filePath, cliReview);

            // Assert
            Assert.AreEqual(8.5f, result.Score);
            Assert.AreEqual("base64encodeddata", result.RawScore);
        }

        [TestMethod]
        public void Map_WithFileLevelCodeSmells_MapsCorrectly()
        {
            // Arrange
            var filePath = "test.cs";
            var cliReview = new CliReviewModel
            {
                Score = 7.0f,
                FileLevelCodeSmells = new List<CliCodeSmellModel>
                {
                    new CliCodeSmellModel
                    {
                        Category = "Large File",
                        Details = "File has 500 lines",
                        Range = new CliRangeModel
                        {
                            Startline = 1,
                            StartColumn = 1,
                            EndLine = 500,
                            EndColumn = 1
                        }
                    }
                }
            };

            // Act
            var result = _mapper.Map(filePath, cliReview);

            // Assert
            Assert.AreEqual(1, result.FileLevel.Count);
            var smell = result.FileLevel.First();
            Assert.AreEqual(filePath, smell.Path);
            Assert.AreEqual("Large File", smell.Category);
            Assert.AreEqual("File has 500 lines", smell.Details);
            Assert.AreEqual(1, smell.Range.StartLine);
            Assert.AreEqual(500, smell.Range.EndLine);
        }

        [TestMethod]
        public void Map_WithFunctionLevelCodeSmells_MapsCorrectly()
        {
            // Arrange
            var filePath = "test.cs";
            var cliReview = new CliReviewModel
            {
                Score = 6.0f,
                FunctionLevelCodeSmells = new List<CliReviewFunctionModel>
                {
                    new CliReviewFunctionModel
                    {
                        Function = "CalculateTotal",
                        Range = new CliRangeModel
                        {
                            Startline = 10,
                            StartColumn = 5,
                            EndLine = 50,
                            EndColumn = 5
                        },
                        CodeSmells = new[]
                        {
                            new CliCodeSmellModel
                            {
                                Category = "Complex Method",
                                Details = "Cyclomatic complexity: 15",
                                Range = new CliRangeModel
                                {
                                    Startline = 15,
                                    StartColumn = 1,
                                    EndLine = 45,
                                    EndColumn = 1
                                }
                            }
                        }
                    }
                }
            };

            // Act
            var result = _mapper.Map(filePath, cliReview);

            // Assert
            Assert.AreEqual(1, result.FunctionLevel.Count);
            var smell = result.FunctionLevel.First();
            Assert.AreEqual(filePath, smell.Path);
            Assert.AreEqual("CalculateTotal", smell.FunctionName);
            Assert.AreEqual("Complex Method", smell.Category);
            Assert.AreEqual("Cyclomatic complexity: 15", smell.Details);
            Assert.AreEqual(15, smell.Range.StartLine);
            Assert.AreEqual(45, smell.Range.EndLine);
            Assert.IsNotNull(smell.FunctionRange);
            Assert.AreEqual(10, smell.FunctionRange.StartLine);
            Assert.AreEqual(50, smell.FunctionRange.EndLine);
        }

        [TestMethod]
        public void Map_FunctionWithNullCodeSmells_SkipsFunction()
        {
            // Arrange
            var filePath = "test.cs";
            var cliReview = new CliReviewModel
            {
                Score = 9.0f,
                FunctionLevelCodeSmells = new List<CliReviewFunctionModel>
                {
                    new CliReviewFunctionModel
                    {
                        Function = "NoSmellsHere",
                        CodeSmells = null
                    }
                }
            };

            // Act
            var result = _mapper.Map(filePath, cliReview);

            // Assert
            Assert.AreEqual(0, result.FunctionLevel.Count);
        }

        [TestMethod]
        public void Map_MultipleFunctionsWithMultipleSmells_MapsAllCorrectly()
        {
            // Arrange
            var filePath = "test.cs";
            var cliReview = new CliReviewModel
            {
                Score = 5.0f,
                FunctionLevelCodeSmells = new List<CliReviewFunctionModel>
                {
                    new CliReviewFunctionModel
                    {
                        Function = "Function1",
                        Range = new CliRangeModel { Startline = 1, EndLine = 20, StartColumn = 1, EndColumn = 1 },
                        CodeSmells = new[]
                        {
                            new CliCodeSmellModel { Category = "Smell1", Range = new CliRangeModel { Startline = 5, EndLine = 10, StartColumn = 1, EndColumn = 1 } },
                            new CliCodeSmellModel { Category = "Smell2", Range = new CliRangeModel { Startline = 15, EndLine = 18, StartColumn = 1, EndColumn = 1 } }
                        }
                    },
                    new CliReviewFunctionModel
                    {
                        Function = "Function2",
                        Range = new CliRangeModel { Startline = 25, EndLine = 40, StartColumn = 1, EndColumn = 1 },
                        CodeSmells = new[]
                        {
                            new CliCodeSmellModel { Category = "Smell3", Range = new CliRangeModel { Startline = 30, EndLine = 35, StartColumn = 1, EndColumn = 1 } }
                        }
                    }
                }
            };

            // Act
            var result = _mapper.Map(filePath, cliReview);

            // Assert
            Assert.AreEqual(3, result.FunctionLevel.Count);
            Assert.AreEqual(2, result.FunctionLevel.Count(s => s.FunctionName == "Function1"));
            Assert.AreEqual(1, result.FunctionLevel.Count(s => s.FunctionName == "Function2"));
        }

        [TestMethod]
        public void Map_CodeSmellModelToCliCodeSmellModel_MapsCorrectly()
        {
            // Arrange
            var codeSmellModel = new CodeSmellModel
            {
                Category = "Deep Nesting",
                Details = "Depth: 5",
                Range = new CodeSmellRangeModel(10, 20, 1, 50)
            };

            // Act
            var result = _mapper.Map(codeSmellModel);

            // Assert
            Assert.AreEqual("Deep Nesting", result.Category);
            Assert.AreEqual("Depth: 5", result.Details);
            Assert.AreEqual(10, result.Range.Startline);
            Assert.AreEqual(20, result.Range.EndLine);
            Assert.AreEqual(1, result.Range.StartColumn);
            Assert.AreEqual(50, result.Range.EndColumn);
        }

        [TestMethod]
        public void Map_NullScore_DefaultsToZero()
        {
            // Arrange
            var filePath = "test.cs";
            var cliReview = new CliReviewModel
            {
                Score = null
            };

            // Act
            var result = _mapper.Map(filePath, cliReview);

            // Assert
            Assert.AreEqual(0f, result.Score);
        }

        [TestMethod]
        public void Map_EmptyFileLevelCodeSmells_ReturnsEmptyList()
        {
            // Arrange
            var filePath = "test.cs";
            var cliReview = new CliReviewModel
            {
                Score = 10.0f,
                FileLevelCodeSmells = new List<CliCodeSmellModel>()
            };

            // Act
            var result = _mapper.Map(filePath, cliReview);

            // Assert
            Assert.IsNotNull(result.FileLevel);
            Assert.AreEqual(0, result.FileLevel.Count);
        }

        [TestMethod]
        public void Map_FunctionWithNullRange_DoesNotSetFunctionRange()
        {
            // Arrange
            var filePath = "test.cs";
            var cliReview = new CliReviewModel
            {
                Score = 7.0f,
                FunctionLevelCodeSmells = new List<CliReviewFunctionModel>
                {
                    new CliReviewFunctionModel
                    {
                        Function = "TestFunction",
                        Range = null,
                        CodeSmells = new[]
                        {
                            new CliCodeSmellModel
                            {
                                Category = "Test",
                                Range = new CliRangeModel { Startline = 1, EndLine = 5, StartColumn = 1, EndColumn = 1 }
                            }
                        }
                    }
                }
            };

            // Act
            var result = _mapper.Map(filePath, cliReview);

            // Assert
            Assert.AreEqual(1, result.FunctionLevel.Count);
            Assert.IsNull(result.FunctionLevel.First().FunctionRange);
        }
    }
}
