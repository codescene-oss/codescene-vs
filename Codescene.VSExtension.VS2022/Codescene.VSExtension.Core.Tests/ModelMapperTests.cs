using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ModelMapperTests
    {
        private readonly ModelMapper _mapper = new ModelMapper();
        private const string DefaultFilePath = "test.cs";

        private static CliRangeModel CreateRange(int startLine, int endLine)
        {
            return new CliRangeModel { StartLine = startLine, EndLine = endLine, StartColumn = 1, EndColumn = 1 };
        }

        private static CliCodeSmellModel CreateCodeSmell(string category, string details = null, int startLine = 1, int endLine = 10)
        {
            return new CliCodeSmellModel { Category = category, Details = details, Range = CreateRange(startLine, endLine) };
        }

        private static CliReviewFunctionModel CreateFunction(string name, int startLine, int endLine, params CliCodeSmellModel[] smells)
        {
            return new CliReviewFunctionModel { Function = name, Range = CreateRange(startLine, endLine), CodeSmells = smells };
        }

        private FileReviewModel MapReview(CliReviewModel cliReview, string path = DefaultFilePath)
        {
            return _mapper.Map(path, cliReview);
        }

        public void Map_NullCliReviewModel_ReturnsEmptyFileReviewModel()
        {
            var result = MapReview(null);

            Assert.IsNotNull(result);
            Assert.AreEqual(DefaultFilePath, result.FilePath);
            Assert.AreEqual(0, result.Score);
            Assert.IsNull(result.RawScore);
        }

        [TestMethod]
        public void Map_CliReviewModelWithScore_MapsScoreCorrectly()
        {
            var cliReview = new CliReviewModel { Score = 8.5f, RawScore = "base64encodeddata" };

            var result = MapReview(cliReview);

            Assert.AreEqual(8.5f, result.Score);
            Assert.AreEqual("base64encodeddata", result.RawScore);
        }

        [TestMethod]
        public void Map_WithFileLevelCodeSmells_MapsCorrectly()
        {
            var cliReview = new CliReviewModel
            {
                Score = 7.0f,
                FileLevelCodeSmells = new List<CliCodeSmellModel> { CreateCodeSmell("Large File", "File has 500 lines", 1, 500) },
            };

            var result = MapReview(cliReview);

            Assert.AreEqual(1, result.FileLevel.Count);
            var smell = result.FileLevel.First();
            Assert.AreEqual("Large File", smell.Category);
            Assert.AreEqual("File has 500 lines", smell.Details);
        }

        [TestMethod]
        public void Map_WithFunctionLevelCodeSmells_MapsCorrectly()
        {
            var cliReview = new CliReviewModel
            {
                Score = 6.0f,
                FunctionLevelCodeSmells = new List<CliReviewFunctionModel>
                {
                    CreateFunction("CalculateTotal", 10, 50, CreateCodeSmell("Complex Method", "CC: 15", 15, 45))
                },
            };

            var result = MapReview(cliReview);

            Assert.AreEqual(1, result.FunctionLevel.Count);
            var smell = result.FunctionLevel.First();
            Assert.AreEqual("CalculateTotal", smell.FunctionName);
            Assert.AreEqual("Complex Method", smell.Category);
        }

        [TestMethod]
        public void Map_FunctionWithNullCodeSmells_SkipsFunction()
        {
            var cliReview = new CliReviewModel
            {
                Score = 9.0f,
                FunctionLevelCodeSmells = new List<CliReviewFunctionModel>
                {
                    new CliReviewFunctionModel { Function = "NoSmellsHere", CodeSmells = null }
                },
            };

            var result = MapReview(cliReview);

            Assert.AreEqual(0, result.FunctionLevel.Count);
        }

        [TestMethod]
        public void Map_MultipleFunctionsWithMultipleSmells_MapsAllCorrectly()
        {
            var cliReview = new CliReviewModel
            {
                Score = 5.0f,
                FunctionLevelCodeSmells = new List<CliReviewFunctionModel>
                {
                    CreateFunction("Function1", 1, 20, CreateCodeSmell("Smell1", null, 5, 10), CreateCodeSmell("Smell2", null, 15, 18)),
                    CreateFunction("Function2", 25, 40, CreateCodeSmell("Smell3", null, 30, 35))
                },
            };

            var result = MapReview(cliReview);

            Assert.AreEqual(3, result.FunctionLevel.Count);
            Assert.AreEqual(2, result.FunctionLevel.Count(s => s.FunctionName == "Function1"));
            Assert.AreEqual(1, result.FunctionLevel.Count(s => s.FunctionName == "Function2"));
        }

        [TestMethod]
        public void Map_CodeSmellModelToCliCodeSmellModel_MapsCorrectly()
        {
            var codeSmellModel = new CodeSmellModel
            {
                Category = "Deep Nesting",
                Details = "Depth: 5",
                Range = new CodeRangeModel(10, 20, 1, 50),
            };

            var result = _mapper.Map(codeSmellModel);

            Assert.AreEqual("Deep Nesting", result.Category);
            Assert.AreEqual("Depth: 5", result.Details);
            Assert.AreEqual(10, result.Range.StartLine);
        }

        [TestMethod]
        public void Map_NullScore_DefaultsToZero()
        {
            var result = MapReview(new CliReviewModel { Score = null });

            Assert.AreEqual(0f, result.Score);
        }

        [TestMethod]
        public void Map_EmptyFileLevelCodeSmells_ReturnsEmptyList()
        {
            var cliReview = new CliReviewModel { Score = 10.0f, FileLevelCodeSmells = new List<CliCodeSmellModel>() };

            var result = MapReview(cliReview);

            Assert.IsNotNull(result.FileLevel);
            Assert.AreEqual(0, result.FileLevel.Count);
        }

        [TestMethod]
        public void Map_FunctionWithNullRange_DoesNotSetFunctionRange()
        {
            var cliReview = new CliReviewModel
            {
                Score = 7.0f,
                FunctionLevelCodeSmells = new List<CliReviewFunctionModel>
                {
                    new CliReviewFunctionModel
                    {
                        Function = "TestFunction",
                        Range = null,
                        CodeSmells = new[] { CreateCodeSmell("Test", null, 1, 5) }
                    }
                },
            };

            var result = MapReview(cliReview);

            Assert.AreEqual(1, result.FunctionLevel.Count);
            Assert.IsNull(result.FunctionLevel.First().FunctionRange);
        }
    }
}
