using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Model;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AceComponentMapperTests
    {
        private AceComponentMapper _mapper;

        [TestInitialize]
        public void Setup()
        {
            _mapper = new AceComponentMapper();
        }

        private static CliRangeModel CreateRange(int startLine = 10, int endLine = 20, int startColumn = 1, int endColumn = 50)
        {
            return new CliRangeModel
            {
                StartLine = startLine,
                EndLine = endLine,
                StartColumn = startColumn,
                EndColumn = endColumn,
            };
        }

        private static FnToRefactorModel CreateFnToRefactor(string name = "TestFunction", CliRangeModel range = null)
        {
            return new FnToRefactorModel
            {
                Name = name,
                Range = range ?? CreateRange(),
            };
        }

        private static CachedRefactoringActionModel CreateCachedModel(
            string path = "test.cs",
            FnToRefactorModel refactorableCandidate = null,
            RefactorResponseModel refactored = null)
        {
            return new CachedRefactoringActionModel
            {
                Path = path,
                RefactorableCandidate = refactorableCandidate ?? CreateFnToRefactor(),
                Refactored = refactored,
            };
        }

        [TestMethod]
        public void Map_CachedModel_ReturnsComponentData()
        {
            var model = CreateCachedModel();

            var result = _mapper.Map(model);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void Map_CachedModel_SetsLoadingToFalse()
        {
            var model = CreateCachedModel();

            var result = _mapper.Map(model);

            Assert.IsFalse(result.Loading);
        }

        [TestMethod]
        public void Map_CachedModel_SetsErrorToNull()
        {
            var model = CreateCachedModel();

            var result = _mapper.Map(model);

            Assert.IsNull(result.Error);
        }

        [TestMethod]
        public void Map_CachedModel_SetsFileDataFileName()
        {
            var model = CreateCachedModel(path: "src/MyClass.cs");

            var result = _mapper.Map(model);

            Assert.AreEqual("src/MyClass.cs", result.FileData.FileName);
        }

        [TestMethod]
        public void Map_CachedModel_SetsAceResultData()
        {
            var refactored = new RefactorResponseModel { Code = "refactored code" };
            var model = CreateCachedModel(refactored: refactored);

            var result = _mapper.Map(model);

            Assert.AreEqual(refactored, result.AceResultData);
        }

        [TestMethod]
        public void Map_CachedModel_SetsFnToRefactor()
        {
            var fnToRefactor = CreateFnToRefactor("MyFunction");
            var model = CreateCachedModel(refactorableCandidate: fnToRefactor);

            var result = _mapper.Map(model);

            Assert.AreEqual(fnToRefactor, result.FnToRefactor);
        }

        [TestMethod]
        public void Map_PathAndFn_ReturnsComponentData()
        {
            var result = _mapper.Map("test.cs", CreateFnToRefactor());

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void Map_PathAndFn_SetsLoadingToTrue()
        {
            var result = _mapper.Map("test.cs", CreateFnToRefactor());

            Assert.IsTrue(result.Loading);
        }

        [TestMethod]
        public void Map_PathAndFn_SetsErrorToNull()
        {
            var result = _mapper.Map("test.cs", CreateFnToRefactor());

            Assert.IsNull(result.Error);
        }

        [TestMethod]
        public void Map_PathAndFn_SetsAceResultDataToNull()
        {
            var result = _mapper.Map("test.cs", CreateFnToRefactor());

            Assert.IsNull(result.AceResultData);
        }

        [TestMethod]
        public void Map_PathAndFn_SetsFileDataFileName()
        {
            var result = _mapper.Map("src/Component.cs", CreateFnToRefactor());

            Assert.AreEqual("src/Component.cs", result.FileData.FileName);
        }

        [TestMethod]
        public void Map_PathFnAndError_ReturnsComponentData()
        {
            var result = _mapper.Map("test.cs", CreateFnToRefactor(), "Some error");

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void Map_PathFnAndError_SetsLoadingToFalse()
        {
            var result = _mapper.Map("test.cs", CreateFnToRefactor(), "Some error");

            Assert.IsFalse(result.Loading);
        }

        [TestMethod]
        public void Map_PathFnAndError_SetsErrorValue()
        {
            var result = _mapper.Map("test.cs", CreateFnToRefactor(), "Refactoring failed");

            Assert.AreEqual("Refactoring failed", result.Error);
        }

        [TestMethod]
        public void Map_PathFnAndError_SetsAceResultDataToNull()
        {
            var result = _mapper.Map("test.cs", CreateFnToRefactor(), "Error");

            Assert.IsNull(result.AceResultData);
        }

        [TestMethod]
        public void Map_RangeStartLine_MappedCorrectly()
        {
            var range = CreateRange(startLine: 15);
            var fn = CreateFnToRefactor(range: range);

            var result = _mapper.Map("test.cs", fn);

            Assert.AreEqual(15, result.FileData.Fn.Range.StartLine);
        }

        [TestMethod]
        public void Map_RangeEndLine_MappedCorrectly()
        {
            var range = CreateRange(endLine: 30);
            var fn = CreateFnToRefactor(range: range);

            var result = _mapper.Map("test.cs", fn);

            Assert.AreEqual(30, result.FileData.Fn.Range.EndLine);
        }

        [TestMethod]
        public void Map_RangeStartColumn_MappedCorrectly()
        {
            var range = CreateRange(startColumn: 5);
            var fn = CreateFnToRefactor(range: range);

            var result = _mapper.Map("test.cs", fn);

            Assert.AreEqual(5, result.FileData.Fn.Range.StartColumn);
        }

        [TestMethod]
        public void Map_RangeEndColumn_MappedCorrectly()
        {
            var range = CreateRange(endColumn: 80);
            var fn = CreateFnToRefactor(range: range);

            var result = _mapper.Map("test.cs", fn);

            Assert.AreEqual(80, result.FileData.Fn.Range.EndColumn);
        }

        [TestMethod]
        public void Map_FunctionName_MappedToFileDataFn()
        {
            var fn = CreateFnToRefactor(name: "ProcessData");

            var result = _mapper.Map("test.cs", fn);

            Assert.AreEqual("ProcessData", result.FileData.Fn.Name);
        }

        [TestMethod]
        public void Map_CachedModel_SetsIsStaleToFalse()
        {
            var model = CreateCachedModel();

            var result = _mapper.Map(model);

            Assert.IsFalse(result.IsStale);
        }

        [TestMethod]
        public void MapAsStale_ReturnsComponentData()
        {
            var model = CreateCachedModel();

            var result = _mapper.MapAsStale(model);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void MapAsStale_SetsIsStaleToTrue()
        {
            var model = CreateCachedModel();

            var result = _mapper.MapAsStale(model);

            Assert.IsTrue(result.IsStale);
        }

        [TestMethod]
        public void MapAsStale_PreservesFileData()
        {
            var model = CreateCachedModel(path: "src/MyClass.cs");

            var result = _mapper.MapAsStale(model);

            Assert.AreEqual("src/MyClass.cs", result.FileData.FileName);
        }

        [TestMethod]
        public void MapAsStale_PreservesAceResultData()
        {
            var refactored = new RefactorResponseModel { Code = "refactored code" };
            var model = CreateCachedModel(refactored: refactored);

            var result = _mapper.MapAsStale(model);

            Assert.AreEqual(refactored, result.AceResultData);
        }

        [TestMethod]
        public void MapAsStale_PreservesFnToRefactor()
        {
            var fnToRefactor = CreateFnToRefactor("MyFunction");
            var model = CreateCachedModel(refactorableCandidate: fnToRefactor);

            var result = _mapper.MapAsStale(model);

            Assert.AreEqual(fnToRefactor, result.FnToRefactor);
        }

        [TestMethod]
        public void MapAsStale_SetsLoadingToFalse()
        {
            var model = CreateCachedModel();

            var result = _mapper.MapAsStale(model);

            Assert.IsFalse(result.Loading);
        }

        [TestMethod]
        public void MapAsStale_SetsErrorToNull()
        {
            var model = CreateCachedModel();

            var result = _mapper.MapAsStale(model);

            Assert.IsNull(result.Error);
        }
    }
}
