// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Application.Refactoring;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Agent;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class RefactoringOrchestratorTelemetryTests
    {
        private const string FilePath = "C:/test/file.cs";

        private FakeAgentSkillRunner _runner;
        private FakeTelemetryManager _telemetry;
        private FakeAsyncTaskScheduler _scheduler;
        private RefactoringOrchestrator _orchestrator;

        [TestInitialize]
        public void Setup()
        {
            _runner = new FakeAgentSkillRunner();
            _telemetry = new FakeTelemetryManager();
            _scheduler = new FakeAsyncTaskScheduler();

            var availability = new Mock<IAgentAvailability>();
            availability.SetupGet(x => x.Usable).Returns(true);
            var keyStore = new Mock<IProviderKeyStore>();
            keyStore.Setup(x => x.GetAll()).Returns(new Dictionary<string, string> { { "anthropic", "key" } });
            var settings = new Mock<ISettingsProvider>();
            settings.SetupGet(x => x.AgentModel).Returns("anthropic/claude-sonnet-5-5");

            _orchestrator = new RefactoringOrchestrator(
                new FakeRefactoringHost(),
                _runner,
                availability.Object,
                keyStore.Object,
                settings.Object,
                _telemetry,
                _scheduler,
                new AceComponentMapper(),
                new Mock<ILogger>().Object);
        }

        [TestMethod]
        public async Task Success_EmitsRequestedInvokedProducedPresented_WithSharedTraceId()
        {
            _runner.Output = CreateAgentOutput("old code", "new code");

            await RunAsync("codeaction");

            CollectionAssert.AreEqual(
                new[] { "ai/refactor/requested", "ai/refactor/agent-invoked", "ai/refactor/suggestion-produced", "ai/refactor/presented" },
                EventNames());
            var traceId = _orchestrator.Current.TaskId;
            Assert.IsTrue(_telemetry.Events.All(e => (string)e.Data["trace-id"] == traceId));
            Assert.IsTrue(_telemetry.Events.All(e => (string)e.Data["source"] == "codeaction"));
        }

        [TestMethod]
        public async Task UnchangedCode_EmitsNoSuggestion_AndIsNotPresented()
        {
            _runner.Output = CreateAgentOutput("old code", "old code");

            await RunAsync();

            CollectionAssert.AreEqual(
                new[] { "ai/refactor/requested", "ai/refactor/agent-invoked", "ai/refactor/no-suggestion" },
                EventNames());
        }

        [TestMethod]
        public async Task UnableToFix_EmitsNoSuggestion()
        {
            var output = CreateAgentOutput("old code", "new code");
            output.FixResult = "unable_to_fix";
            _runner.Output = output;

            await RunAsync();

            Assert.Contains("ai/refactor/no-suggestion", EventNames());
            Assert.DoesNotContain("ai/refactor/presented", EventNames());
        }

        [TestMethod]
        [DataRow(AgentFailureKind.Auth, "auth")]
        [DataRow(AgentFailureKind.Timeout, "timeout")]
        [DataRow(AgentFailureKind.Generic, "agent-error")]
        public async Task AgentFailure_EmitsFailedWithReason(AgentFailureKind kind, string reason)
        {
            _runner.ThrowOnRun = new AgentFailure(kind, "message with C:/secret/path");

            await RunAsync();

            var failed = _telemetry.Events.Single(e => e.Name == "ai/refactor/failed");
            Assert.AreEqual(reason, failed.Data["reason"]);
            Assert.IsFalse(failed.Data.Values.OfType<string>().Any(v => v.Contains("secret")));
        }

        [TestMethod]
        public async Task UnexpectedException_EmitsFailedAgentError()
        {
            _runner.ThrowOnRun = new InvalidOperationException("boom");

            await RunAsync();

            Assert.AreEqual("agent-error", _telemetry.Events.Single(e => e.Name == "ai/refactor/failed").Data["reason"]);
        }

        [TestMethod]
        public async Task UserCancelDuringRun_EmitsCancelled()
        {
            _runner.OnRunStart = () =>
            {
                _orchestrator.Cancel();
                _runner.ThrowOnRun = new AgentFailure(AgentFailureKind.Cancelled, "Agent process was cancelled");
            };

            await RunAsync();

            Assert.Contains("ai/refactor/cancelled", EventNames());
            Assert.DoesNotContain("ai/refactor/failed", EventNames());
        }

        [TestMethod]
        public async Task CancelWhileAgentCompletesAnyway_EmitsCancelled()
        {
            _runner.Output = CreateAgentOutput("old code", "new code");
            _runner.IgnoresCancellation = true;
            _runner.OnRunStart = () => _orchestrator.Cancel();

            await RunAsync();

            CollectionAssert.AreEqual(
                new[] { "ai/refactor/requested", "ai/refactor/agent-invoked", "ai/refactor/cancelled" },
                EventNames());
        }

        [TestMethod]
        [DataRow(Constants.Telemetry.AISTAGEDIFFSHOWN)]
        [DataRow(Constants.Telemetry.AISTAGECOPIED)]
        [DataRow(Constants.Telemetry.AISTAGEAPPLIED)]
        [DataRow(Constants.Telemetry.AISTAGEDISCARDED)]
        public async Task ReportUserAction_OnPresentedSuggestion_EmitsStage(string stage)
        {
            _runner.Output = CreateAgentOutput("old code", "new code");
            await RunAsync();

            _orchestrator.ReportUserAction(stage);
            await _scheduler.WaitForAllAsync();

            var evt = _telemetry.Events.Single(e => e.Name == "ai/refactor/" + stage);
            Assert.AreEqual(_orchestrator.Current.TaskId, evt.Data["trace-id"]);
        }

        [TestMethod]
        public async Task ReportUserAction_WithoutResponse_EmitsNothing()
        {
            _orchestrator.ReportUserAction(Constants.Telemetry.AISTAGECOPIED);
            await _scheduler.WaitForAllAsync();

            Assert.IsEmpty(_telemetry.Events);
        }

        private static FnToRefactorModel CreateFnToRefactor()
        {
            return new FnToRefactorModel
            {
                Name = "TestFunction",
                Body = "function body\nold code\nmore code",
                FileType = "cs",
                FunctionType = "cs",
                Range = new CliRangeModel { StartLine = 1, EndLine = 3, StartColumn = 1, EndColumn = 1 },
                RefactoringTargets = new[] { new RefactoringTargetModel { Category = "complex-method", Line = 5 } },
            };
        }

        private static AgentOutput CreateAgentOutput(string search, string replace)
        {
            return new AgentOutput
            {
                TaskId = "test-task-id",
                FixResult = "fix_proposed",
                Confidence = "high",
                Summary = "Simplified method",
                Reasoning = "Reduced complexity",
                Changes = new[]
                {
                    new AgentChange
                    {
                        File = "file.cs",
                        ChangeType = "partial",
                        Replacements = new[] { new AgentReplacement { Search = search, Replace = replace } },
                    },
                },
            };
        }

        private async Task RunAsync(string source = "codeaction")
        {
            await _orchestrator.RequestAsync(source, FilePath, CreateFnToRefactor());
            await _scheduler.WaitForAllAsync();
            await _scheduler.WaitForAllAsync();
        }

        private string[] EventNames() => _telemetry.Events.Select(e => e.Name).ToArray();
    }
}
