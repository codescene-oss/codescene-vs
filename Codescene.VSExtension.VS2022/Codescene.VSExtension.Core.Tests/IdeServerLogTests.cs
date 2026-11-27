// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Models.Cli.Telemetry;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class IdeServerLogTests
    {
        [TestMethod]
        public void SummarizeRequest_UnknownMethod_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, IdeServerLog.SummarizeRequest("cs-ide/unknown", new object()));
        }

        [TestMethod]
        public void SummarizeResponse_NullResult_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, IdeServerLog.SummarizeResponse("cs-ide/device-id", null));
        }

        [TestMethod]
        public void ReviewSummary_NullResult_ReturnsEmpty()
        {
            Assert.AreEqual(string.Empty, IdeServerLog.ReviewSummary(null));
        }

        [TestMethod]
        public void ReviewSummary_IncludesFields()
        {
            var summary = IdeServerLog.ReviewSummary(new CliReviewModel
            {
                Score = 9.5f,
                FileLevelCodeSmells = new List<CliCodeSmellModel> { new CliCodeSmellModel() },
                FunctionLevelCodeSmells = new List<CliReviewFunctionModel>(),
                GitBlobSha = "sha-1",
                CodeHealthRulesError = new CliCodeHealthRulesErrorModel(),
            });

            Assert.Contains("score=", summary);
            Assert.Contains("fileSmells=1", summary);
            Assert.Contains("hasRulesError=True", summary);
        }

        [TestMethod]
        public void DeltaSummary_NullResult_ReportsNull()
        {
            Assert.Contains("result=null", IdeServerLog.DeltaSummary(null));
        }

        [TestMethod]
        public void DeltaSummary_IncludesScores()
        {
            var summary = IdeServerLog.DeltaSummary(new DeltaResponseModel
            {
                OldScore = 8,
                NewScore = 9,
                ScoreChange = 1,
                FileLevelFindings = Array.Empty<ChangeDetailModel>(),
                FunctionLevelFindings = Array.Empty<FunctionFindingModel>(),
            });

            Assert.Contains("oldScore=8", summary);
            Assert.Contains("newScore=9", summary);
        }

        [TestMethod]
        public void SummarizeRequest_CheckRulesAndInventory()
        {
            Assert.Contains("repoRoot=", IdeServerLog.SummarizeRequest(
                "cs-ide/check-rules",
                new CheckRulesRequestParams { RepoRoot = "C:/repo", Path = "a.cs" }));
            Assert.Contains("repoRoot=", IdeServerLog.SummarizeRequest(
                "cs-ide/getWatchInventory",
                new WatchInventoryRequestParams { RepoRoot = "C:/repo" }));
        }

        [TestMethod]
        public void SummarizeRequest_TelemetryUsesEventName()
        {
            var summary = IdeServerLog.SummarizeRequest(
                "cs-ide/telemetry",
                new TelemetryRequestParams { Event = new TelemetryEvent { EventName = "opened" } });
            Assert.Contains("eventName=opened", summary);

            var jobjectSummary = IdeServerLog.SummarizeRequest(
                "cs-ide/telemetry",
                new TelemetryRequestParams { Event = JObject.Parse(@"{""event-name"":""saved""}") });
            Assert.Contains("eventName=saved", jobjectSummary);
        }

        [TestMethod]
        public void SummarizeResponse_RulesAndInventory()
        {
            var rules = IdeServerLog.SummarizeResponse(
                "cs-ide/check-rules",
                new CheckRulesResponse { Failed = true, ParsingErrors = new List<string> { "err" } });
            Assert.Contains("failed=True", rules);

            var inventory = IdeServerLog.SummarizeResponse(
                "cs-ide/getWatchInventory",
                new WatchInventory { RepoRoot = "C:/repo", Files = new List<string> { "a.cs" } });
            Assert.Contains("count=1", inventory);
        }

        [TestMethod]
        public void SendingAndReceived_IncludeSummarySuffix()
        {
            Assert.StartsWith("[cs-ide] sending id=1 method=cs-ide/telemetry", IdeServerLog.Sending(1, "cs-ide/telemetry", "eventName=test"));
            Assert.Contains("eventName=test", IdeServerLog.Sending(1, "cs-ide/telemetry", "eventName=test"));
            Assert.StartsWith("[cs-ide] received id=2 method=cs-ide/device-id", IdeServerLog.Received(2, "cs-ide/device-id", 12, string.Empty));
        }
    }
}
