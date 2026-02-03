// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Ace;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Interfaces.Util;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AceManagerTests
    {
        private Mock<ILogger> _mockLogger;
        private Mock<ICliExecutor> _mockExecutor;
        private Mock<ITelemetryManager> _mockTelemetryManager;
        private Mock<IAceStateService> _mockAceStateService;
        private Mock<INetworkService> _mockNetworkService;
        private AceManager _aceManager;

        [TestInitialize]
        public void Setup()
        {
            _mockLogger = new Mock<ILogger>();
            _mockExecutor = new Mock<ICliExecutor>();
            _mockTelemetryManager = new Mock<ITelemetryManager>();
            _mockAceStateService = new Mock<IAceStateService>();
            _mockNetworkService = new Mock<INetworkService>();

            // Default to network available
            _mockNetworkService.Setup(n => n.IsNetworkAvailable()).Returns(true);

            _aceManager = new AceManager(
                _mockLogger.Object,
                _mockExecutor.Object,
                _mockTelemetryManager.Object,
                _mockAceStateService.Object,
                _mockNetworkService.Object);

            // Clear static state between tests
            AceManager.LastRefactoring = null;
        }

        [TestCleanup]
        public void Cleanup()
        {
            AceManager.LastRefactoring = null;
        }

        [TestMethod]
        public void GetRefactorableFunctions_DelegatesToExecutor()
        {
            // Arrange
            var fileName = "test.cs";
            var fileContent = "public class Test { }";
            var codeSmells = new List<CliCodeSmellModel>();
            var preflight = new PreFlightResponseModel();
            var expectedResult = new List<FnToRefactorModel>
            {
                new FnToRefactorModel { Name = "TestFunction" },
            };

            _mockExecutor.Setup(x => x.FnsToRefactorFromCodeSmells(fileName, fileContent, codeSmells, preflight))
                .Returns(expectedResult);

            // Act
            var result = _aceManager.GetRefactorableFunctionsFromCodeSmells(fileName, fileContent, codeSmells, preflight);

            // Assert
            Assert.IsNotNull(result);
            Assert.HasCount(1, result);
            Assert.AreEqual("TestFunction", result[0].Name);
            _mockExecutor.Verify(x => x.FnsToRefactorFromCodeSmells(fileName, fileContent, codeSmells, preflight), Times.Once);
        }

        [TestMethod]
        public void GetRefactorableFunctions_ReturnsNullWhenExecutorReturnsNull()
        {
            // Arrange
            var fileName = "test.cs";
            var fileContent = "code";
            var codeSmells = new List<CliCodeSmellModel>();
            var preflight = new PreFlightResponseModel();

            _mockExecutor.Setup(x => x.FnsToRefactorFromCodeSmells(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IList<CliCodeSmellModel>>(), It.IsAny<PreFlightResponseModel>()))
                .Returns((IList<FnToRefactorModel>)null);

            // Act
            var result = _aceManager.GetRefactorableFunctionsFromCodeSmells(fileName, fileContent, codeSmells, preflight);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Refactor_WhenExecutorReturnsNull_LogsInfoAndReturnsNull()
        {
            // Arrange
            var path = "test.cs";
            var fnToRefactor = new FnToRefactorModel { Name = "TestMethod" };
            var entryPoint = "toolbar";

            _mockExecutor.Setup(x => x.PostRefactoring(It.IsAny<FnToRefactorModel>(), It.IsAny<bool>(), It.IsAny<string>()))
                .Returns((RefactorResponseModel)null);

            // Act
            var result = _aceManager.Refactor(path, fnToRefactor, entryPoint);

            // Assert
            Assert.IsNull(result);
            _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("TestMethod") && s.Contains("test.cs"))), Times.Once);
        }

        [TestMethod]
        public void Refactor_WhenExceptionThrown_LogsErrorSetsErrorAndRethrows()
        {
            // Arrange
            var path = "test.cs";
            var fnToRefactor = new FnToRefactorModel { Name = "TestMethod" };
            var entryPoint = "toolbar";
            var expectedException = new Exception("Refactoring failed");

            _mockExecutor.Setup(x => x.PostRefactoring(It.IsAny<FnToRefactorModel>(), It.IsAny<bool>(), It.IsAny<string>()))
                .Throws(expectedException);

            // Act & Assert
            var ex = Assert.Throws<Exception>(() => _aceManager.Refactor(path, fnToRefactor, entryPoint));

            Assert.AreEqual("Refactoring failed", ex.Message);
            _mockLogger.Verify(l => l.Error(It.Is<string>(s => s.Contains("TestMethod")), expectedException), Times.Once);
            _mockAceStateService.Verify(s => s.SetError(expectedException), Times.Once);
        }

        [TestMethod]
        public void GetCachedRefactoredCode_WhenNoCachedResult_ReturnsNull()
        {
            // Arrange
            AceManager.LastRefactoring = null;

            // Act
            var result = _aceManager.GetCachedRefactoredCode();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetCachedRefactoredCode_WhenCachedResultExists_ReturnsCachedResult()
        {
            // Arrange
            var cachedResult = new CachedRefactoringActionModel
            {
                Path = "cached/path.cs",
                RefactorableCandidate = new FnToRefactorModel { Name = "CachedFunction" },
                Refactored = new RefactorResponseModel { Code = "refactored code", TraceId = "trace-123" },
            };
            AceManager.LastRefactoring = cachedResult;

            // Act
            var result = _aceManager.GetCachedRefactoredCode();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("cached/path.cs", result.Path);
            Assert.AreEqual("CachedFunction", result.RefactorableCandidate.Name);
            Assert.AreEqual("refactored code", result.Refactored.Code);
        }

        [TestMethod]
        public void Refactor_WhenSuccessful_ClearsErrorAndCachesResult()
        {
            // Arrange
            var path = "test.cs";
            var fnToRefactor = new FnToRefactorModel { Name = "TestMethod" };
            var entryPoint = "toolbar";
            var refactoredResponse = new RefactorResponseModel { Code = "refactored", TraceId = "trace-456" };

            _mockExecutor.Setup(x => x.PostRefactoring(It.IsAny<FnToRefactorModel>(), It.IsAny<bool>(), It.IsAny<string>()))
                .Returns(refactoredResponse);

            // Act
            var result = _aceManager.Refactor(path, fnToRefactor, entryPoint);

            // Assert
            Assert.IsNotNull(result);
            _mockAceStateService.Verify(s => s.ClearError(), Times.Once);
            Assert.AreEqual(path, result.Path);
            Assert.AreEqual(fnToRefactor, result.RefactorableCandidate);
            Assert.AreEqual(refactoredResponse, result.Refactored);
            Assert.AreEqual(result, AceManager.LastRefactoring);
        }

        [TestMethod]
        public void Refactor_WhenSuccessfulAndWasOffline_TransitionsToEnabled()
        {
            // Arrange
            var path = "test.cs";
            var fnToRefactor = new FnToRefactorModel { Name = "TestMethod" };
            var entryPoint = "toolbar";
            var refactoredResponse = new RefactorResponseModel { Code = "refactored", TraceId = "trace-789" };

            _mockAceStateService.Setup(s => s.CurrentState).Returns(AceState.Offline);
            _mockExecutor.Setup(x => x.PostRefactoring(It.IsAny<FnToRefactorModel>(), It.IsAny<bool>(), It.IsAny<string>()))
                .Returns(refactoredResponse);

            // Act
            var result = _aceManager.Refactor(path, fnToRefactor, entryPoint);

            // Assert
            Assert.IsNotNull(result);
            _mockAceStateService.Verify(s => s.SetState(AceState.Enabled), Times.Once);
        }

        [TestMethod]
        public void Refactor_WhenNetworkUnavailable_SetsOfflineStateAndReturnsNull()
        {
            // Arrange
            var path = "test.cs";
            var fnToRefactor = new FnToRefactorModel { Name = "TestMethod" };
            var entryPoint = "toolbar";

            _mockNetworkService.Setup(n => n.IsNetworkAvailable()).Returns(false);

            // Act
            var result = _aceManager.Refactor(path, fnToRefactor, entryPoint);

            // Assert
            Assert.IsNull(result);
            Assert.IsNull(AceManager.LastRefactoring);
            _mockAceStateService.Verify(s => s.SetState(AceState.Offline), Times.Once);
            _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("No internet connection"))), Times.Once);
            _mockExecutor.Verify(x => x.PostRefactoring(It.IsAny<FnToRefactorModel>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
        }

        [TestMethod]
        public void GetRefactorableFunctionsFromDelta_DelegatesToExecutor()
        {
            // Arrange
            var fileName = "test.cs";
            var fileContent = "public class Test { }";
            var deltaResponse = new DeltaResponseModel { ScoreChange = (decimal)-0.5f };
            var preflight = new PreFlightResponseModel();
            var expectedResult = new List<FnToRefactorModel>
            {
                new FnToRefactorModel { Name = "TestFunction" },
            };

            _mockExecutor.Setup(x => x.FnsToRefactorFromDelta(fileName, fileContent, deltaResponse, preflight))
                .Returns(expectedResult);

            // Act
            var result = _aceManager.GetRefactorableFunctionsFromDelta(fileName, fileContent, deltaResponse, preflight);

            // Assert
            Assert.IsNotNull(result);
            Assert.HasCount(1, result);
            Assert.AreEqual("TestFunction", result[0].Name);
            _mockExecutor.Verify(x => x.FnsToRefactorFromDelta(fileName, fileContent, deltaResponse, preflight), Times.Once);
        }

        [TestMethod]
        public void GetRefactorableFunctionsFromDelta_ReturnsNullWhenExecutorReturnsNull()
        {
            // Arrange
            var fileName = "test.cs";
            var fileContent = "code";
            var deltaResponse = new DeltaResponseModel { ScoreChange = (decimal)-0.5f };
            var preflight = new PreFlightResponseModel();

            _mockExecutor.Setup(x => x.FnsToRefactorFromDelta(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DeltaResponseModel>(), It.IsAny<PreFlightResponseModel>()))
                .Returns((IList<FnToRefactorModel>)null);

            // Act
            var result = _aceManager.GetRefactorableFunctionsFromDelta(fileName, fileContent, deltaResponse, preflight);

            // Assert
            Assert.IsNull(result);
        }
    }
}
