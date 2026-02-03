// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CodeHealthMonitorMapperTests
    {
        private Mock<IPreflightManager> _mockPreflightManager;
        private CodeHealthMonitorMapper _mapper;

        [TestInitialize]
        public void Setup()
        {
            _mockPreflightManager = new Mock<IPreflightManager>();
            _mockPreflightManager.Setup(x => x.GetAutoRefactorConfig())
                .Returns(new AutoRefactorConfig { Activated = true, Visible = true, Disabled = false });
            _mapper = new CodeHealthMonitorMapper(_mockPreflightManager.Object);
        }

        [TestMethod]
        public void Map_EmptyDictionary_ReturnsEmptyFileDeltaData()
        {
            var fileDeltas = new Dictionary<string, DeltaResponseModel>();

            var result = _mapper.Map(fileDeltas);

            Assert.IsNotNull(result);
            Assert.IsEmpty(result.FileDeltaData);
        }

        [TestMethod]
        public void Map_SingleFile_MapsCorrectly()
        {
            var fileDeltas = new Dictionary<string, DeltaResponseModel>
            {
                { "test.cs", CreateDeltaResponse() },
            };

            var result = _mapper.Map(fileDeltas);

            Assert.HasCount(1, result.FileDeltaData);
            Assert.AreEqual("test.cs", result.FileDeltaData[0].File.FileName);
        }

        [TestMethod]
        public void Map_MultipleFiles_MapsAllCorrectly()
        {
            var fileDeltas = new Dictionary<string, DeltaResponseModel>
            {
                { "file1.cs", CreateDeltaResponse() },
                { "file2.cs", CreateDeltaResponse() },
            };

            var result = _mapper.Map(fileDeltas);

            Assert.HasCount(2, result.FileDeltaData);
        }

        [TestMethod]
        public void Map_DeltaScores_MappedCorrectly()
        {
            var result = _mapper.Map(CreateSingleFileDelta("test.cs", CreateDeltaResponseWithScores(9.0m, 7.5m, -1.5m)));

            var delta = result.FileDeltaData[0].Delta;
            Assert.AreEqual(9.0m, delta.OldScore);
            Assert.AreEqual(7.5m, delta.NewScore);
            Assert.AreEqual(-1.5m, delta.ScoreChange);
        }

        [TestMethod]
        public void Map_FileLevelFindings_MappedCorrectly()
        {
            var findings = new[] { CreateChangeDetail(category: "Large File", description: "File has 500 lines", line: null) };
            var result = _mapper.Map(CreateSingleFileDelta("test.cs", CreateDeltaResponseWithFileFindings(findings)));

            var fileLevelFindings = result.FileDeltaData[0].Delta.FileLevelFindings;
            Assert.HasCount(1, fileLevelFindings);
            Assert.AreEqual("Large File", fileLevelFindings[0].Category);
            Assert.AreEqual("File has 500 lines", fileLevelFindings[0].Description);
        }

        [TestMethod]
        public void Map_NullFileLevelFindings_ReturnsEmptyList() =>
            AssertFileLevelFindingsEmpty("test.cs", CreateDeltaResponseWithFileFindings(null));

        [TestMethod]
        public void Map_FunctionLevelFindings_MappedCorrectly()
        {
            var findings = new[] { CreateFunctionFinding(functionName: "ProcessData") };
            var result = _mapper.Map(CreateSingleFileDelta("test.cs", CreateDeltaResponseWithFunctionFindings(findings)));

            var functionFindings = result.FileDeltaData[0].Delta.FunctionLevelFindings;
            Assert.HasCount(1, functionFindings);
            Assert.AreEqual("ProcessData", functionFindings[0].Function.Name);
        }

        [TestMethod]
        public void Map_NullFunctionLevelFindings_ReturnsEmptyList() =>
            AssertFunctionLevelFindingsEmpty("test.cs", CreateDeltaResponseWithFunctionFindings(null));

        [TestMethod]
        public void Map_FunctionWithNullRange_HandledCorrectly()
        {
            var findings = new[]
            {
                new FunctionFindingModel
            {
                Function = new FunctionInfoModel { Name = "TestFn", Range = null },
                ChangeDetails = new[] { CreateChangeDetail() },
            },
            };
            var result = _mapper.Map(CreateSingleFileDelta("test.cs", CreateDeltaResponseWithFunctionFindings(findings)));

            Assert.IsNull(result.FileDeltaData[0].Delta.FunctionLevelFindings[0].Function.Range);
        }

        [TestMethod]
        public void Map_FunctionRange_MappedCorrectly()
        {
            var range = new CliRangeModel { StartLine = 15, EndLine = 30, StartColumn = 5, EndColumn = 80 };
            var result = _mapper.Map(CreateSingleFileDelta("test.cs", CreateDeltaResponseWithFunctionFindings(new[] { CreateFunctionFinding(range: range) })));

            var functionRange = result.FileDeltaData[0].Delta.FunctionLevelFindings[0].Function.Range;
            Assert.AreEqual(15, functionRange.StartLine);
            Assert.AreEqual(30, functionRange.EndLine);
            Assert.AreEqual(5, functionRange.StartColumn);
            Assert.AreEqual(80, functionRange.EndColumn);
        }

        [TestMethod]
        public void Map_RefactorableFn_MappedCorrectly()
        {
            var refactorableFn = new FnToRefactorModel
            {
                Name = "RefactorMe",
                Body = "function body",
                NippyB64 = "encoded",
                FileType = "cs",
                Range = new CliRangeModel { StartLine = 10, EndLine = 20, StartColumn = 1, EndColumn = 50 },
                RefactoringTargets = new[] { new RefactoringTargetModel { Category = "Complex", Line = 15 } },
            };
            var result = _mapper.Map(CreateSingleFileDelta("test.cs", CreateDeltaResponseWithFunctionFindings(new[] { CreateFunctionFinding(refactorableFn: refactorableFn) })));

            var mappedRefactorableFn = result.FileDeltaData[0].Delta.FunctionLevelFindings[0].RefactorableFn;
            Assert.IsNotNull(mappedRefactorableFn);
            Assert.AreEqual("RefactorMe", mappedRefactorableFn.Name);
            Assert.AreEqual("function body", mappedRefactorableFn.Body);
            Assert.AreEqual("encoded", mappedRefactorableFn.NippyB64);
        }

        [TestMethod]
        public void Map_NullRefactorableFn_MappedAsNull()
        {
            var result = _mapper.Map(CreateSingleFileDelta("test.cs", CreateDeltaResponseWithFunctionFindings(new[] { CreateFunctionFinding(refactorableFn: null) })));
            Assert.IsNull(result.FileDeltaData[0].Delta.FunctionLevelFindings[0].RefactorableFn);
        }

        [TestMethod]
        public void Map_AutoRefactorConfig_ComesFromPreflightManager()
        {
            var expectedConfig = new AutoRefactorConfig { Activated = false, Visible = true, Disabled = true };
            _mockPreflightManager.Setup(x => x.GetAutoRefactorConfig()).Returns(expectedConfig);

            var result = _mapper.Map(new Dictionary<string, DeltaResponseModel>());

            Assert.AreEqual(expectedConfig.Activated, result.AutoRefactor.Activated);
            Assert.AreEqual(expectedConfig.Visible, result.AutoRefactor.Visible);
            Assert.AreEqual(expectedConfig.Disabled, result.AutoRefactor.Disabled);
        }

        [TestMethod]
        public void Map_ChangeDetailLine_MappedCorrectly()
        {
            var result = _mapper.Map(CreateSingleFileDelta("test.cs", CreateDeltaResponseWithFileFindings(new[] { CreateChangeDetail(line: 42) })));
            Assert.AreEqual(42, result.FileDeltaData[0].Delta.FileLevelFindings[0].Line);
        }

        [TestMethod]
        public void Map_ChangeDetailChangeType_MappedCorrectly()
        {
            var findings = new[] { new ChangeDetailModel { Category = "Test", ChangeType = ChangeType.Improved } };
            var result = _mapper.Map(CreateSingleFileDelta("test.cs", CreateDeltaResponseWithFileFindings(findings)));
            Assert.AreEqual(ChangeType.Improved, result.FileDeltaData[0].Delta.FileLevelFindings[0].ChangeType);
        }

        private static DeltaResponseModel CreateDeltaResponse() =>
            new DeltaResponseModel { OldScore = 8.0m, NewScore = 7.0m, ScoreChange = -1.0m };

        private static DeltaResponseModel CreateDeltaResponseWithScores(decimal oldScore, decimal newScore, decimal scoreChange) =>
            new DeltaResponseModel { OldScore = oldScore, NewScore = newScore, ScoreChange = scoreChange };

        private static DeltaResponseModel CreateDeltaResponseWithFileFindings(ChangeDetailModel[] findings) =>
            new DeltaResponseModel { OldScore = 8.0m, NewScore = 7.0m, ScoreChange = -1.0m, FileLevelFindings = findings };

        private static DeltaResponseModel CreateDeltaResponseWithFunctionFindings(FunctionFindingModel[] findings) =>
            new DeltaResponseModel { OldScore = 8.0m, NewScore = 7.0m, ScoreChange = -1.0m, FunctionLevelFindings = findings };

        private static ChangeDetailModel CreateChangeDetail(string category = "Complex Method", string description = "Complexity increased", int? line = 10) =>
            new ChangeDetailModel { Category = category, Description = description, Line = line, ChangeType = ChangeType.Degraded };

        private static FunctionFindingModel CreateFunctionFinding(string functionName = "TestFunction", CliRangeModel? range = null, FnToRefactorModel? refactorableFn = null) =>
            new FunctionFindingModel
            {
                Function = new FunctionInfoModel
                {
                    Name = functionName,
                    Range = range ?? new CliRangeModel { StartLine = 10, EndLine = 20, StartColumn = 1, EndColumn = 50 },
                },
                ChangeDetails = new[] { CreateChangeDetail() },
                RefactorableFn = refactorableFn,
            };

        private Dictionary<string, DeltaResponseModel> CreateSingleFileDelta(string fileName, DeltaResponseModel delta) =>
            new Dictionary<string, DeltaResponseModel> { { fileName, delta } };

        private void AssertFileLevelFindingsEmpty(string fileName, DeltaResponseModel delta)
        {
            var result = _mapper.Map(CreateSingleFileDelta(fileName, delta));
            Assert.IsNotNull(result.FileDeltaData[0].Delta.FileLevelFindings);
            Assert.IsEmpty(result.FileDeltaData[0].Delta.FileLevelFindings);
        }

        private void AssertFunctionLevelFindingsEmpty(string fileName, DeltaResponseModel delta)
        {
            var result = _mapper.Map(CreateSingleFileDelta(fileName, delta));
            Assert.IsNotNull(result.FileDeltaData[0].Delta.FunctionLevelFindings);
            Assert.IsEmpty(result.FileDeltaData[0].Delta.FunctionLevelFindings);
        }
    }
}
