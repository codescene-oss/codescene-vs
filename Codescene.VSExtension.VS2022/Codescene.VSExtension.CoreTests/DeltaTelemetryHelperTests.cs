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
            var scenario = TelemetryScenario.FileAdded("newfile.cs");
            await AssertTelemetryEventSent(scenario, Constants.Telemetry.MONITOR_FILE_ADDED);
        }

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileRemovedFromCache_SendsMonitorFileRemovedEvent()
        {
            var scenario = TelemetryScenario.FileRemoved("removedfile.cs");
            await AssertTelemetryEventSent(scenario, Constants.Telemetry.MONITOR_FILE_REMOVED);
        }

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileUpdatedInCache_SendsMonitorFileUpdatedEvent()
        {
            var scenario = TelemetryScenario.FileUpdated("existingfile.cs");
            await AssertTelemetryEventSent(scenario, Constants.Telemetry.MONITOR_FILE_UPDATED);
        }

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileNotInEitherSnapshot_DoesNotSendEvent()
        {
            var (previousSnapshot, currentCache) = CreateSnapshots(new[] { "otherfile.cs" }, new[] { "otherfile.cs" });
            var entry = new DeltaCacheEntry("unknownfile.cs", "old", "new", CreateDeltaResponse(0.0m));

            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(previousSnapshot, currentCache, entry, _mockTelemetryManager.Object);
            await Task.Delay(100);

            _mockTelemetryManager.Verify(
                t => t.SendTelemetry(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()),
                Times.Never);
        }

        private async Task AssertTelemetryEventSent(TelemetryScenario scenario, string expectedEvent)
        {
            var (previousSnapshot, currentCache) = CreateSnapshots(scenario.PreviousFiles, scenario.CurrentFiles);
            var entry = new DeltaCacheEntry(scenario.EntryFile, "old", "new", scenario.EntryDelta ?? CreateDeltaResponse(0.5m));

            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(previousSnapshot, currentCache, entry, _mockTelemetryManager.Object);
            await Task.Delay(100);

            _mockTelemetryManager.Verify(t => t.SendTelemetry(expectedEvent, It.IsAny<Dictionary<string, object>>()), Times.Once);
        }

        private (Dictionary<string, DeltaResponseModel> previous, Dictionary<string, DeltaResponseModel> current) CreateSnapshots(string[] previousFiles, string[] currentFiles)
        {
            var previous = new Dictionary<string, DeltaResponseModel>();
            foreach (var file in previousFiles)
                previous[file] = CreateDeltaResponse(-0.5m);

            var current = new Dictionary<string, DeltaResponseModel>();
            foreach (var file in currentFiles)
                current[file] = CreateDeltaResponse(-1.0m);

            return (previous, current);
        }

        private class TelemetryScenario
        {
            public string[] PreviousFiles { get; private set; }
            public string[] CurrentFiles { get; private set; }
            public string EntryFile { get; private set; }
            public DeltaResponseModel EntryDelta { get; private set; }

            public static TelemetryScenario FileAdded(string file) =>
                new TelemetryScenario { PreviousFiles = new string[0], CurrentFiles = new[] { file }, EntryFile = file };

            public static TelemetryScenario FileRemoved(string file) =>
                new TelemetryScenario { PreviousFiles = new[] { file }, CurrentFiles = new string[0], EntryFile = file, EntryDelta = null };

            public static TelemetryScenario FileUpdated(string file) =>
                new TelemetryScenario { PreviousFiles = new[] { file }, CurrentFiles = new[] { file }, EntryFile = file };
        }

        #endregion

        #region HandleDeltaTelemetryEvent - Additional Data Tests

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileAdded_IncludesScoreChangeInAdditionalData()
        {
            var delta = CreateDeltaResponse(-2.5m);
            var capturedData = await CaptureAdditionalDataForFileAdded(delta);

            Assert.IsNotNull(capturedData);
            Assert.IsTrue(capturedData.ContainsKey("scoreChange"));
            Assert.AreEqual(-2.5m, capturedData["scoreChange"]);
        }

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileAdded_IncludesIssueCountInAdditionalData()
        {
            var delta = CreateDeltaWithFindings(fileLevelCount: 2, functionLevelCount: 1);
            var capturedData = await CaptureAdditionalDataForFileAdded(delta);

            Assert.IsNotNull(capturedData);
            Assert.IsTrue(capturedData.ContainsKey("nIssues"));
            Assert.AreEqual(3, capturedData["nIssues"]); // 2 file-level + 1 function-level
        }

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileAdded_IncludesRefactorableFunctionCount()
        {
            var delta = CreateDeltaWithRefactorableFunctions(refactorableCount: 2, nonRefactorableCount: 1);
            var capturedData = await CaptureAdditionalDataForFileAdded(delta);

            Assert.IsNotNull(capturedData);
            Assert.IsTrue(capturedData.ContainsKey("nRefactorableFunctions"));
            Assert.AreEqual(2, capturedData["nRefactorableFunctions"]);
        }

        [TestMethod]
        public async Task HandleDeltaTelemetryEvent_FileRemoved_DoesNotIncludeAdditionalData()
        {
            var (previousSnapshot, _) = CreateSnapshots(previousFiles: new[] { "removedfile.cs" }, currentFiles: new string[] { });
            var entry = new DeltaCacheEntry("removedfile.cs", "old", "new", CreateDeltaResponse(-1.0m));

            Dictionary<string, object> capturedData = null;
            SetupTelemetryCapture(data => capturedData = data);

            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(previousSnapshot, new Dictionary<string, DeltaResponseModel>(), entry, _mockTelemetryManager.Object);
            await Task.Delay(100);

            Assert.IsNull(capturedData);
        }

        private async Task<Dictionary<string, object>> CaptureAdditionalDataForFileAdded(DeltaResponseModel delta)
        {
            var uniqueFile = "newfile_" + System.Guid.NewGuid() + ".cs";
            var previousSnapshot = new Dictionary<string, DeltaResponseModel>();
            var currentCache = new Dictionary<string, DeltaResponseModel> { { uniqueFile, delta } };
            var entry = new DeltaCacheEntry(uniqueFile, "old", "new", delta);

            Dictionary<string, object> capturedData = null;
            SetupTelemetryCapture(data => capturedData = data);

            DeltaTelemetryHelper.HandleDeltaTelemetryEvent(previousSnapshot, currentCache, entry, _mockTelemetryManager.Object);
            await Task.Delay(200);

            return capturedData;
        }

        private void SetupTelemetryCapture(System.Action<Dictionary<string, object>> captureAction)
        {
            _mockTelemetryManager.Setup(t => t.SendTelemetry(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Callback<string, Dictionary<string, object>>((_, data) => captureAction(data));
        }

        private static DeltaResponseModel CreateDeltaWithFindings(int fileLevelCount, int functionLevelCount)
        {
            var fileFindings = new ChangeDetailModel[fileLevelCount];
            for (int i = 0; i < fileLevelCount; i++)
                fileFindings[i] = new ChangeDetailModel();

            var functionFindings = new FunctionFindingModel[functionLevelCount];
            for (int i = 0; i < functionLevelCount; i++)
                functionFindings[i] = new FunctionFindingModel();

            return new DeltaResponseModel
            {
                ScoreChange = -1.0m,
                FileLevelFindings = fileFindings,
                FunctionLevelFindings = functionFindings
            };
        }

        private static DeltaResponseModel CreateDeltaWithRefactorableFunctions(int refactorableCount, int nonRefactorableCount)
        {
            var findings = new List<FunctionFindingModel>();
            for (int i = 0; i < refactorableCount; i++)
                findings.Add(new FunctionFindingModel { RefactorableFn = new FnToRefactorModel { Name = $"Func{i}" } });
            for (int i = 0; i < nonRefactorableCount; i++)
                findings.Add(new FunctionFindingModel { RefactorableFn = null });

            return new DeltaResponseModel
            {
                ScoreChange = -1.0m,
                FileLevelFindings = new ChangeDetailModel[0],
                FunctionLevelFindings = findings.ToArray()
            };
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
