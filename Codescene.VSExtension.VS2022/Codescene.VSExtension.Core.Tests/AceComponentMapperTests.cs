// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Refactoring;
using Codescene.VSExtension.Core.Models.WebComponent.Data;

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

        [TestMethod]
        public void MapSession_ReturnsComponentData()
        {
            var session = CreateSession();

            var result = _mapper.MapSession(session);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void MapSession_SetsLoadingToFalse()
        {
            var session = CreateSession();

            var result = _mapper.MapSession(session);

            Assert.IsFalse(result.Loading);
        }

        [TestMethod]
        public void MapSession_SetsErrorToNull()
        {
            var session = CreateSession();

            var result = _mapper.MapSession(session);

            Assert.IsNull(result.Error);
        }

        [TestMethod]
        public void MapSession_SetsFileDataFileName()
        {
            var session = CreateSession(path: "src/MyClass.cs");

            var result = _mapper.MapSession(session);

            Assert.AreEqual("src/MyClass.cs", result.FileData.FileName);
        }

        [TestMethod]
        public void MapSession_SetsAceResultData()
        {
            var refactored = new RefactorResponseModel { Code = "refactored code" };
            var session = CreateSession(response: refactored);

            var result = _mapper.MapSession(session);

            Assert.AreEqual(refactored, result.AceResultData);
        }

        [TestMethod]
        public void MapSession_SetsFnToRefactor()
        {
            var fnToRefactor = CreateFnToRefactor("MyFunction");
            var session = CreateSession(function: fnToRefactor);

            var result = _mapper.MapSession(session);

            Assert.AreEqual(fnToRefactor, result.FileData.FnToRefactor);
        }

        [TestMethod]
        public void MapLoading_ReturnsComponentData()
        {
            var result = _mapper.MapLoading("test.cs", CreateFnToRefactor());

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void MapLoading_SetsLoadingToTrue()
        {
            var result = _mapper.MapLoading("test.cs", CreateFnToRefactor());

            Assert.IsTrue(result.Loading);
        }

        [TestMethod]
        public void MapLoading_SetsErrorToNull()
        {
            var result = _mapper.MapLoading("test.cs", CreateFnToRefactor());

            Assert.IsNull(result.Error);
        }

        [TestMethod]
        public void MapLoading_SetsAceResultDataToNull()
        {
            var result = _mapper.MapLoading("test.cs", CreateFnToRefactor());

            Assert.IsNull(result.AceResultData);
        }

        [TestMethod]
        public void MapLoading_SetsFileDataFileName()
        {
            var result = _mapper.MapLoading("src/Component.cs", CreateFnToRefactor());

            Assert.AreEqual("src/Component.cs", result.FileData.FileName);
        }

        [TestMethod]
        public void MapError_ReturnsComponentData()
        {
            var result = _mapper.MapError("test.cs", CreateFnToRefactor(), "Some error");

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void MapError_SetsLoadingToFalse()
        {
            var result = _mapper.MapError("test.cs", CreateFnToRefactor(), "Some error");

            Assert.IsFalse(result.Loading);
        }

        [TestMethod]
        public void MapError_SetsErrorValue()
        {
            var result = _mapper.MapError("test.cs", CreateFnToRefactor(), "Refactoring failed");

            Assert.AreEqual("Refactoring failed", result.Error);
        }

        [TestMethod]
        public void MapError_SetsAceResultDataToNull()
        {
            var result = _mapper.MapError("test.cs", CreateFnToRefactor(), "Error");

            Assert.IsNull(result.AceResultData);
        }

        [TestMethod]
        public void MapLoading_RangeStartLine_MappedCorrectly()
        {
            var range = CreateRange(startLine: 15);
            var fn = CreateFnToRefactor(range: range);

            var result = _mapper.MapLoading("test.cs", fn);

            Assert.AreEqual(15, result.FileData.Fn.Range.StartLine);
        }

        [TestMethod]
        public void MapLoading_RangeEndLine_MappedCorrectly()
        {
            var range = CreateRange(endLine: 30);
            var fn = CreateFnToRefactor(range: range);

            var result = _mapper.MapLoading("test.cs", fn);

            Assert.AreEqual(30, result.FileData.Fn.Range.EndLine);
        }

        [TestMethod]
        public void MapLoading_RangeStartColumn_MappedCorrectly()
        {
            var range = CreateRange(startColumn: 5);
            var fn = CreateFnToRefactor(range: range);

            var result = _mapper.MapLoading("test.cs", fn);

            Assert.AreEqual(5, result.FileData.Fn.Range.StartColumn);
        }

        [TestMethod]
        public void MapLoading_RangeEndColumn_MappedCorrectly()
        {
            var range = CreateRange(endColumn: 80);
            var fn = CreateFnToRefactor(range: range);

            var result = _mapper.MapLoading("test.cs", fn);

            Assert.AreEqual(80, result.FileData.Fn.Range.EndColumn);
        }

        [TestMethod]
        public void MapLoading_FunctionName_MappedToFileDataFn()
        {
            var fn = CreateFnToRefactor(name: "ProcessData");

            var result = _mapper.MapLoading("test.cs", fn);

            Assert.AreEqual("ProcessData", result.FileData.Fn.Name);
        }

        [TestMethod]
        public void MapSession_SetsIsStaleToFalse()
        {
            var session = CreateSession();

            var result = _mapper.MapSession(session);

            Assert.IsFalse(result.IsStale);
        }

        [TestMethod]
        public void MapSession_Stale_ReturnsComponentData()
        {
            var session = CreateSession(state: RefactoringSessionState.Stale);

            var result = _mapper.MapSession(session);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void MapSession_Stale_SetsIsStaleToTrue()
        {
            var session = CreateSession(state: RefactoringSessionState.Stale);

            var result = _mapper.MapSession(session);

            Assert.IsTrue(result.IsStale);
        }

        [TestMethod]
        public void MapSession_Stale_PreservesFileData()
        {
            var session = CreateSession(path: "src/MyClass.cs", state: RefactoringSessionState.Stale);

            var result = _mapper.MapSession(session);

            Assert.AreEqual("src/MyClass.cs", result.FileData.FileName);
        }

        [TestMethod]
        public void MapSession_Stale_PreservesAceResultData()
        {
            var refactored = new RefactorResponseModel { Code = "refactored code" };
            var session = CreateSession(response: refactored, state: RefactoringSessionState.Stale);

            var result = _mapper.MapSession(session);

            Assert.AreEqual(refactored, result.AceResultData);
        }

        [TestMethod]
        public void MapSession_Stale_PreservesFnToRefactor()
        {
            var fnToRefactor = CreateFnToRefactor("MyFunction");
            var session = CreateSession(function: fnToRefactor, state: RefactoringSessionState.Stale);

            var result = _mapper.MapSession(session);

            Assert.AreEqual(fnToRefactor, result.FileData.FnToRefactor);
        }

        [TestMethod]
        public void MapSession_Stale_SetsLoadingToFalse()
        {
            var session = CreateSession(state: RefactoringSessionState.Stale);

            var result = _mapper.MapSession(session);

            Assert.IsFalse(result.Loading);
        }

        [TestMethod]
        public void MapSession_Stale_SetsErrorToNull()
        {
            var session = CreateSession(state: RefactoringSessionState.Stale);

            var result = _mapper.MapSession(session);

            Assert.IsNull(result.Error);
        }

        [TestMethod]
        public void MapRange_MapsPropertiesCorrectly()
        {
            var range = CreateRange(5, 15, 2, 40);
            var result = AceComponentMapper.MapRange(range);
            Assert.AreEqual((5, 15, 2, 40), (result.StartLine, result.EndLine, result.StartColumn, result.EndColumn));
        }

        [TestMethod]
        public void MapRange_NullInput_ReturnsNull()
        {
            var result = AceComponentMapper.MapRange((CliRangeModel)null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void MapCodeRange_MapsPropertiesCorrectly()
        {
            var range = CreateCodeRange(5, 15, 2, 40);
            var result = AceComponentMapper.MapRange(range);
            Assert.AreEqual((5, 15, 2, 40), (result.StartLine, result.EndLine, result.StartColumn, result.EndColumn));
        }

        [TestMethod]
        public void MapCodeRange_NullInput_ReturnsNull()
        {
            var result = AceComponentMapper.MapRange((CodeRangeModel)null);
            Assert.IsNull(result);
        }

        [TestMethod]
        public void MapSession_Running_IsLoading()
        {
            var session = CreateSession(state: RefactoringSessionState.Running);

            var result = _mapper.MapSession(session);

            Assert.IsTrue(result.Loading);
            Assert.IsNull(result.ProgressMessage);
        }

        [TestMethod]
        public void MapSession_Progress_SetsProgressMessage()
        {
            var session = CreateSession(state: RefactoringSessionState.Running);
            session.SetProgress("Analyzing code...");

            var result = _mapper.MapSession(session);

            Assert.IsFalse(result.Loading);
            Assert.AreEqual("Analyzing code...", result.ProgressMessage);
        }

        [TestMethod]
        public void MapSession_Presented_SetsAceResultData()
        {
            var response = new RefactorResponseModel { Code = "refactored code", TraceId = "trace-123" };
            var session = CreateSession(response: response, state: RefactoringSessionState.Presented);

            var result = _mapper.MapSession(session);

            AssertNotLoadingStaleOrFailed(result);
            Assert.IsNull(result.ProgressMessage);
            Assert.AreEqual(response, result.AceResultData);
        }

        [TestMethod]
        public void MapSession_AuthError()
        {
            var session = CreateSession(state: RefactoringSessionState.Failed);
            session.SetError("auth");

            var result = _mapper.MapSession(session);

            Assert.IsFalse(result.Loading);
            Assert.AreEqual("auth", result.Error);
            Assert.IsNull(result.AceResultData);
        }

        [TestMethod]
        public void MapSession_GenericError()
        {
            var session = CreateSession(state: RefactoringSessionState.Failed);
            session.SetError("generic");

            var result = _mapper.MapSession(session);

            Assert.IsFalse(result.Loading);
            Assert.AreEqual("generic", result.Error);
            Assert.IsNull(result.AceResultData);
        }

        [TestMethod]
        public void MapSession_Stale_SetsIsStale()
        {
            var response = new RefactorResponseModel { Code = "old code" };
            var session = CreateSession(response: response, state: RefactoringSessionState.Presented);
            session.MarkStale();

            var result = _mapper.MapSession(session);

            Assert.IsTrue(result.IsStale);
            Assert.IsFalse(result.Loading);
            Assert.AreEqual(response, result.AceResultData);
        }

        [TestMethod]
        public void MapSession_Cancelled_ReturnsAppropriatePayload()
        {
            var session = CreateSession(state: RefactoringSessionState.Cancelled);

            var result = _mapper.MapSession(session);

            AssertNotLoadingStaleOrFailed(result);
            Assert.IsNull(result.AceResultData);
        }

        [TestMethod]
        public void MapSession_FileData_CarriesFnToRefactor()
        {
            var fnToRefactor = CreateFnToRefactor("TestFunction");
            var session = CreateSession(function: fnToRefactor);

            var result = _mapper.MapSession(session);

            Assert.IsNotNull(result.FileData);
            Assert.AreEqual(fnToRefactor, result.FileData.FnToRefactor);
            Assert.AreEqual("TestFunction", result.FileData.Fn.Name);
        }

        [TestMethod]
        public void MapSession_Null_ReturnsNull()
        {
            var result = _mapper.MapSession(null);

            Assert.IsNull(result);
        }

        private static void AssertNotLoadingStaleOrFailed(AceComponentData result)
        {
            Assert.IsFalse(result.Loading);
            Assert.IsFalse(result.IsStale);
            Assert.IsNull(result.Error);
        }

        private static CodeRangeModel CreateCodeRange(int startLine = 10, int endLine = 20, int startColumn = 1, int endColumn = 50)
        {
            return new CodeRangeModel(startLine, endLine, startColumn, endColumn);
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

        private static FnToRefactorModel CreateFnToRefactor(string name = "TestFunction", CliRangeModel? range = null)
        {
            return new FnToRefactorModel
            {
                Name = name,
                Range = range ?? CreateRange(),
            };
        }

        private static RefactoringSession CreateSession(
            string path = "test.cs",
            FnToRefactorModel? function = null,
            RefactorResponseModel? response = null,
            RefactoringSessionState state = RefactoringSessionState.Presented)
        {
            var session = new RefactoringSession(path, function ?? CreateFnToRefactor(), "test-source");

            if (state == RefactoringSessionState.Presented || state == RefactoringSessionState.Stale)
            {
                session.SetResponse(response ?? new RefactorResponseModel());
            }

            if (state == RefactoringSessionState.Stale)
            {
                session.MarkStale();
            }

            if (state == RefactoringSessionState.Cancelled)
            {
                session.Cancel();
            }

            return session;
        }
    }
}
