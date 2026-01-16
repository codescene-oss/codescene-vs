using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class DeltaTelemetryHelperTests
    {
        private Mock<ITelemetryManager> _mockTelemetryManager;

        [TestInitialize]
        public void Setup()
        {
            _mockTelemetryManager = new Mock<ITelemetryManager>();
        }

        #region HandleDeltaTelemetryEvent - Event Name Selection Tests

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileAddedToCache_SendsMonitorFileAddedEvent()
        {
            // Arrange
            var previousSnapshot = new Dictionary<string, DeltaResponseModel>();
            var currentCache = new Dictionary<string, DeltaResponseModel>
            {
                { "newfile.cs", CreateDeltaResponse(0.5m) }
            };
            var entry = new DeltaCacheEntry("newfile.cs", "old", "new", CreateDeltaResponse(0.5m));

            // Act
            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(previousSnapshot, currentCache, entry, _mockTelemetryManager.Object);

            // Wait for async Task.Run to complete
            await Task.Delay(100);

            // Assert
            _mockTelemetryManager.Verify(
                t => t.SendTelemetry(Constants.Telemetry.MONITOR_FILE_ADDED, It.IsAny<Dictionary<string, object>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileRemovedFromCache_SendsMonitorFileRemovedEvent()
        {
            // Arrange
            var previousSnapshot = new Dictionary<string, DeltaResponseModel>
            {
                { "removedfile.cs", CreateDeltaResponse(-1.0m) }
            };
            var currentCache = new Dictionary<string, DeltaResponseModel>(); // File no longer present
            var entry = new DeltaCacheEntry("removedfile.cs", "old", "new", null);

            // Act
            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(previousSnapshot, currentCache, entry, _mockTelemetryManager.Object);

            // Wait for async Task.Run to complete
            await Task.Delay(100);

            // Assert
            _mockTelemetryManager.Verify(
                t => t.SendTelemetry(Constants.Telemetry.MONITOR_FILE_REMOVED, It.IsAny<Dictionary<string, object>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileUpdatedInCache_SendsMonitorFileUpdatedEvent()
        {
            // Arrange
            var previousSnapshot = new Dictionary<string, DeltaResponseModel>
            {
                { "existingfile.cs", CreateDeltaResponse(-0.5m) }
            };
            var currentCache = new Dictionary<string, DeltaResponseModel>
            {
                { "existingfile.cs", CreateDeltaResponse(-1.0m) } // Updated
            };
            var entry = new DeltaCacheEntry("existingfile.cs", "old", "new", CreateDeltaResponse(-1.0m));

            // Act
            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(previousSnapshot, currentCache, entry, _mockTelemetryManager.Object);

            // Wait for async Task.Run to complete
            await Task.Delay(100);

            // Assert
            _mockTelemetryManager.Verify(
                t => t.SendTelemetry(Constants.Telemetry.MONITOR_FILE_UPDATED, It.IsAny<Dictionary<string, object>>()),
                Times.Once);
        }

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileNotInEitherSnapshot_DoesNotSendEvent()
        {
            // Arrange
            var previousSnapshot = new Dictionary<string, DeltaResponseModel>
            {
                { "otherfile.cs", CreateDeltaResponse(0.0m) }
            };
            var currentCache = new Dictionary<string, DeltaResponseModel>
            {
                { "otherfile.cs", CreateDeltaResponse(0.0m) }
            };
            // Entry for a file that's not in either snapshot
            var entry = new DeltaCacheEntry("unknownfile.cs", "old", "new", CreateDeltaResponse(0.0m));

            // Act
            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(previousSnapshot, currentCache, entry, _mockTelemetryManager.Object);

            // Wait for async Task.Run to complete
            await Task.Delay(100);

            // Assert - no telemetry should be sent
            _mockTelemetryManager.Verify(
                t => t.SendTelemetry(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()),
                Times.Never);
        }

        #endregion

        #region HandleDeltaTelemetryEvent - Additional Data Tests

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileAdded_IncludesScoreChangeInAdditionalData()
        {
            // Arrange
            var previousSnapshot = new Dictionary<string, DeltaResponseModel>();
            var delta = CreateDeltaResponse(-2.5m);
            var currentCache = new Dictionary<string, DeltaResponseModel>
            {
                { "newfile.cs", delta }
            };
            var entry = new DeltaCacheEntry("newfile.cs", "old", "new", delta);

            Dictionary<string, object> capturedData = null;
            _mockTelemetryManager.Setup(t => t.SendTelemetry(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Callback<string, Dictionary<string, object>>((_, data) => capturedData = data);

            // Act
            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(previousSnapshot, currentCache, entry, _mockTelemetryManager.Object);

            // Wait for async Task.Run to complete
            await Task.Delay(100);

            // Assert
            Assert.IsNotNull(capturedData);
            Assert.IsTrue(capturedData.ContainsKey("scoreChange"));
            Assert.AreEqual(-2.5m, capturedData["scoreChange"]);
        }

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileAdded_IncludesIssueCountInAdditionalData()
        {
            // Arrange
            var previousSnapshot = new Dictionary<string, DeltaResponseModel>();
            var delta = new DeltaResponseModel
            {
                ScoreChange = -1.0m,
                FileLevelFindings = new ChangeDetailModel[] { new ChangeDetailModel(), new ChangeDetailModel() },
                FunctionLevelFindings = new FunctionFindingModel[] { new FunctionFindingModel() }
            };
            var currentCache = new Dictionary<string, DeltaResponseModel>
            {
                { "newfile.cs", delta }
            };
            var entry = new DeltaCacheEntry("newfile.cs", "old", "new", delta);

            Dictionary<string, object> capturedData = null;
            _mockTelemetryManager.Setup(t => t.SendTelemetry(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Callback<string, Dictionary<string, object>>((_, data) => capturedData = data);

            // Act
            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(previousSnapshot, currentCache, entry, _mockTelemetryManager.Object);

            // Wait for async Task.Run to complete
            await Task.Delay(100);

            // Assert
            Assert.IsNotNull(capturedData);
            Assert.IsTrue(capturedData.ContainsKey("nIssues"));
            Assert.AreEqual(3, capturedData["nIssues"]); // 2 file-level + 1 function-level
        }

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileAdded_IncludesRefactorableFunctionCount()
        {
            // Arrange
            var previousSnapshot = new Dictionary<string, DeltaResponseModel>();
            var delta = new DeltaResponseModel
            {
                ScoreChange = -1.0m,
                FileLevelFindings = new ChangeDetailModel[0],
                FunctionLevelFindings = new FunctionFindingModel[]
                {
                    new FunctionFindingModel { RefactorableFn = new FnToRefactorModel { Name = "Func1" } },
                    new FunctionFindingModel { RefactorableFn = new FnToRefactorModel { Name = "Func2" } },
                    new FunctionFindingModel { RefactorableFn = null } // Not refactorable
                }
            };
            var currentCache = new Dictionary<string, DeltaResponseModel>
            {
                { "newfile.cs", delta }
            };
            var entry = new DeltaCacheEntry("newfile.cs", "old", "new", delta);

            Dictionary<string, object> capturedData = null;
            _mockTelemetryManager.Setup(t => t.SendTelemetry(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Callback<string, Dictionary<string, object>>((_, data) => capturedData = data);

            // Act
            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(previousSnapshot, currentCache, entry, _mockTelemetryManager.Object);

            // Wait for async Task.Run to complete
            await Task.Delay(100);

            // Assert
            Assert.IsNotNull(capturedData);
            Assert.IsTrue(capturedData.ContainsKey("nRefactorableFunctions"));
            Assert.AreEqual(2, capturedData["nRefactorableFunctions"]); // Only 2 have RefactorableFn set
        }

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileRemoved_DoesNotIncludeAdditionalData()
        {
            // Arrange
            var previousSnapshot = new Dictionary<string, DeltaResponseModel>
            {
                { "removedfile.cs", CreateDeltaResponse(-1.0m) }
            };
            var currentCache = new Dictionary<string, DeltaResponseModel>();
            var entry = new DeltaCacheEntry("removedfile.cs", "old", "new", CreateDeltaResponse(-1.0m));

            Dictionary<string, object> capturedData = null;
            _mockTelemetryManager.Setup(t => t.SendTelemetry(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Callback<string, Dictionary<string, object>>((_, data) => capturedData = data);

            // Act
            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(previousSnapshot, currentCache, entry, _mockTelemetryManager.Object);

            // Wait for async Task.Run to complete
            await Task.Delay(100);

            // Assert - REMOVED events should not have additional data
            Assert.IsNull(capturedData);
        }

        #endregion

        #region HandleDeltaTelemetryEvent - Null Safety Tests

        [TestMethod]
        public void HandleDeltaTelemetryEvent_NullTelemetryManager_DoesNotThrow()
        {
            // Arrange
            var previousSnapshot = new Dictionary<string, DeltaResponseModel>();
            var currentCache = new Dictionary<string, DeltaResponseModel>
            {
                { "file.cs", CreateDeltaResponse(0.0m) }
            };
            var entry = new DeltaCacheEntry("file.cs", "old", "new", CreateDeltaResponse(0.0m));

            // Act & Assert - should not throw
            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(previousSnapshot, currentCache, entry, null);
        }

        #endregion

        #region Helper Methods

        private static DeltaResponseModel CreateDeltaResponse(decimal scoreChange)
        {
            return new DeltaResponseModel
            {
                ScoreChange = scoreChange,
                OldScore = 8.0m,
                NewScore = 8.0m + scoreChange,
                FileLevelFindings = new ChangeDetailModel[0],
                FunctionLevelFindings = new FunctionFindingModel[0]
            };
        }

        #endregion
    }
}
