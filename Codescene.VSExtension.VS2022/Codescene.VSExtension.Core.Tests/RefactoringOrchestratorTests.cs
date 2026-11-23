// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Application.Refactoring;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Refactoring;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Agent;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Refactoring;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Codescene.VSExtension.Core.Consts.WebComponentConstants;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class RefactoringOrchestratorTests
    {
        private FakeRefactoringHost _host;
        private FakeAgentSkillRunner _runner;
        private Mock<IAgentAvailability> _availability;
        private Mock<IProviderKeyStore> _keyStore;
        private Mock<ISettingsProvider> _settings;
        private FakeTelemetryManager _telemetry;
        private FakeAsyncTaskScheduler _scheduler;
        private AceComponentMapper _mapper;
        private Mock<ILogger> _logger;
        private RefactoringOrchestrator _orchestrator;

        [TestInitialize]
        public void Setup()
        {
            _host = new FakeRefactoringHost();
            _runner = new FakeAgentSkillRunner();
            _availability = new Mock<IAgentAvailability>();
            _keyStore = new Mock<IProviderKeyStore>();
            _settings = new Mock<ISettingsProvider>();
            _telemetry = new FakeTelemetryManager();
            _scheduler = new FakeAsyncTaskScheduler();
            _mapper = new AceComponentMapper();
            _logger = new Mock<ILogger>();

            _availability.SetupGet(x => x.Usable).Returns(true);
            _keyStore.Setup(x => x.GetAll()).Returns(new Dictionary<string, string> { { "anthropic", "key" } });
            _settings.SetupGet(x => x.AgentModel).Returns("anthropic/claude-sonnet-5-5");

            _orchestrator = new RefactoringOrchestrator(
                _host,
                _runner,
                _availability.Object,
                _keyStore.Object,
                _settings.Object,
                _telemetry,
                _scheduler,
                _mapper,
                _logger.Object);
        }

        [TestMethod]
        public async Task NotUsable_DoesNothing()
        {
            _availability.SetupGet(x => x.Usable).Returns(false);

            var fn = CreateFnToRefactor();
            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", fn);

            Assert.AreEqual(0, _host.ShowReviewCount);
            Assert.IsFalse(_runner.WasCalled);
        }

        [TestMethod]
        public async Task Dirty_Cancelled_NoRun()
        {
            _host.IsDirtyResult = true;
            _host.ConfirmSaveResult = false;

            var fn = CreateFnToRefactor();
            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", fn);

            CollectionAssert.AreEqual(new[] { "confirm-save:file.cs" }, _host.Calls);
            Assert.IsFalse(_runner.WasCalled);
        }

        [TestMethod]
        public async Task Dirty_Confirmed_SavesThenRuns()
        {
            _host.IsDirtyResult = true;
            _host.ConfirmSaveResult = true;
            _runner.Output = CreateAgentOutput();

            var fn = CreateFnToRefactor();
            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", fn);

            CollectionAssert.AreEqual(new[] { "confirm-save:file.cs", "save:C:/test/file.cs", "show-review" }, _host.Calls);
            await _scheduler.WaitForAllAsync();
            Assert.IsTrue(_runner.WasCalled);
        }

        [TestMethod]
        public async Task MissingKey_OffersSetApiKey_NoSpawn()
        {
            _keyStore.Setup(x => x.GetAll()).Returns(new Dictionary<string, string>());

            var fn = CreateFnToRefactor();
            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", fn);

            Assert.IsTrue(_host.OfferSetApiKeyCalled);
            StringAssert.Contains(_host.OfferSetApiKeyMessage, "Anthropic");
            StringAssert.Contains(_host.OfferSetApiKeyMessage, "anthropic/claude-sonnet-5-5");
            Assert.IsFalse(_runner.WasCalled);
        }

        [TestMethod]
        public async Task Run_PassesRenderCodeFixSkill_WorkDir_AndBuilderInput()
        {
            _runner.Output = CreateAgentOutput();

            var targets = new[]
            {
                new RefactoringTargetModel { Category = "complex-method", Line = 42 },
            };
            var fn = CreateFnToRefactor("TestFunction", targets);

            var testFile = typeof(RefactoringOrchestratorTests).Assembly.Location;
            await _orchestrator.RequestAsync("codeaction", testFile, fn);
            await _scheduler.WaitForAllAsync();

            Assert.IsTrue(_runner.WasCalled);
            Assert.AreEqual(AgentSkill.RenderCodeFix.Name, _runner.Skill.Name);
            Assert.IsNotNull(_runner.WorkDir);

            var expectedInput = AgentInputBuilder.ForRefactoring("task-id", testFile, fn);
            var actual = JObject.Parse(JsonConvert.SerializeObject(_runner.Input));
            var expected = JObject.Parse(JsonConvert.SerializeObject(expectedInput));

            AssertSameFields(expected, actual, "@context", "file", "line", "smell");
            Assert.IsNotNull(actual["task_id"]);
            Assert.AreEqual((42, "complex-method"), (actual["line"].Value<int>(), actual["smell"].Value<string>()));
        }

        [TestMethod]
        public async Task NoTargets_ShowsGenericError_NoSpawn()
        {
            var fn = CreateFnToRefactor("TestFunction", new RefactoringTargetModel[0]);

            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", fn);
            await _scheduler.WaitForAllAsync();

            Assert.IsFalse(_runner.WasCalled);
            AssertFailedWith(AceViewErrorTypes.GENERIC);
        }

        [TestMethod]
        [DataRow("codeaction")]
        [DataRow("codelens (code-health-monitor)")]
        [DataRow("retry")]
        public async Task Start_SendsRequestedTelemetryWithSource(string source)
        {
            _runner.Output = CreateAgentOutput();

            var fn = CreateFnToRefactor();
            await _orchestrator.RequestAsync(source, "C:/test/file.cs", fn);
            await _scheduler.WaitForAllAsync();

            var evt = SingleEvent(Constants.Telemetry.ACEREFACTORREQUESTED);
            Assert.AreEqual(source, evt.Data["source"]);
            Assert.AreEqual(false, evt.Data["skipCache"]);
        }

        [TestMethod]
        public async Task Progress_UpdatesView()
        {
            _runner.Output = CreateAgentOutput();
            string progressMessage = null;
            _runner.OnProgress = handler =>
            {
                progressMessage = "Analyzing code...";
                handler(progressMessage);
            };

            var fn = CreateFnToRefactor();
            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", fn);

            for (int i = 0; i < 10; i++)
            {
                await _scheduler.WaitForAllAsync();
            }

            Assert.IsNotNull(progressMessage, "Progress handler was not called");
            Assert.IsTrue(_host.UpdateReviewCount > 0, $"Expected at least 1 update, got {_host.UpdateReviewCount}");
        }

        [TestMethod]
        public async Task Success_PresentsAndSendsPresentedTelemetry()
        {
            _runner.Output = CreateAgentOutput();

            var fn = CreateFnToRefactor();
            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", fn);
            await _scheduler.WaitForAllAsync();
            await _scheduler.WaitForAllAsync();

            Assert.AreEqual(RefactoringSessionState.Presented, _orchestrator.Current.State);
            var response = _orchestrator.Current.Response;
            Assert.IsNotNull(response);
            Assert.AreEqual(("function body\nnew code\nmore code", 3), (response.Code, response.Confidence.Level));

            var evt = SingleEvent(Constants.Telemetry.ACEREFACTORPRESENTED);
            Assert.AreEqual(3, evt.Data["confidence"]);
            Assert.AreEqual(false, evt.Data["isCached"]);
        }

        [TestMethod]
        public async Task AuthFailure_ShowsAuthError()
        {
            _runner.ThrowOnRun = new AgentFailure(AgentFailureKind.Auth, "Auth failed");

            var fn = CreateFnToRefactor();
            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", fn);
            await _scheduler.WaitForAllAsync();

            AssertFailedWith(AceViewErrorTypes.AUTH);
        }

        [TestMethod]
        public async Task GenericFailure_ShowsGenericError()
        {
            _runner.ThrowOnRun = new AgentFailure(AgentFailureKind.Generic, "Generic failure");

            var fn = CreateFnToRefactor();
            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", fn);
            await _scheduler.WaitForAllAsync();

            AssertFailedWith(AceViewErrorTypes.GENERIC);
        }

        [TestMethod]
        public async Task UnexpectedException_ShowsGenericError()
        {
            _runner.ThrowOnRun = new InvalidOperationException("Something went wrong");

            var fn = CreateFnToRefactor();
            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", fn);
            await _scheduler.WaitForAllAsync();

            AssertFailedWith(AceViewErrorTypes.GENERIC);
        }

        [TestMethod]
        public async Task NewRequest_CancelsRunning_OldUpdatesNothing()
        {
            _runner.Output = CreateAgentOutput();
            RefactoringSession firstSession = null;

            var fn1 = CreateFnToRefactor("FirstFunction");
            var fn2 = CreateFnToRefactor("SecondFunction");

            await _orchestrator.RequestAsync("codeaction", "C:/test/file1.cs", fn1);
            firstSession = _orchestrator.Current;

            await _orchestrator.RequestAsync("codeaction", "C:/test/file2.cs", fn2);
            await _scheduler.WaitForAllAsync();

            Assert.AreEqual(RefactoringSessionState.Cancelled, firstSession?.State);
            Assert.AreEqual("SecondFunction", _orchestrator.Current?.Function.Name);
        }

        [TestMethod]
        public async Task Cancel_StopsRunAndUpdatesNothing()
        {
            _runner.Output = CreateAgentOutput();

            var fn = CreateFnToRefactor();
            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", fn);

            var session = _orchestrator.Current;
            Assert.IsNotNull(session);

            _orchestrator.Cancel();
            await _scheduler.WaitForAllAsync();

            Assert.IsTrue(session.CancellationTokenSource.IsCancellationRequested);
            AssertCancelledWithoutUpdates(session);
        }

        [TestMethod]
        public async Task CancelDuringRun_AgentReportsCancelled_UpdatesNothing()
        {
            _runner.OnRunStart = () =>
            {
                _orchestrator.Cancel();
                _runner.ThrowOnRun = new AgentFailure(AgentFailureKind.Cancelled, "Agent process was cancelled");
            };

            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", CreateFnToRefactor());
            var session = _orchestrator.Current;
            await _scheduler.WaitForAllAsync();

            AssertCancelledWithoutUpdates(session);
        }

        [TestMethod]
        public async Task CancelDuringRun_AgentCompletesAnyway_UpdatesNothing()
        {
            _runner.Output = CreateAgentOutput();
            _runner.IgnoresCancellation = true;
            _runner.OnRunStart = () => _orchestrator.Cancel();

            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", CreateFnToRefactor());
            var session = _orchestrator.Current;
            await _scheduler.WaitForAllAsync();

            AssertCancelledWithoutUpdates(session);
            Assert.IsFalse(_telemetry.Events.Any(e => e.Name == Constants.Telemetry.ACEREFACTORPRESENTED));
        }

        [TestMethod]
        public async Task TaskId_IsTraceId()
        {
            _runner.Output = CreateAgentOutput();

            var fn = CreateFnToRefactor();
            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", fn);
            await _scheduler.WaitForAllAsync();

            var session = _orchestrator.Current;
            Assert.IsNotNull(session);
            StringAssert.StartsWith(session.TaskId, "refactor-");
            Assert.IsNotNull(session.Response);
            Assert.AreEqual(session.TaskId, session.Response.TraceId);
        }

        private static FnToRefactorModel CreateFnToRefactor(string name = "TestFunction", RefactoringTargetModel[] targets = null)
        {
            return new FnToRefactorModel
            {
                Name = name,
                Body = "function body\nold code\nmore code",
                FileType = "cs",
                FunctionType = "cs",
                Range = new CliRangeModel { StartLine = 1, EndLine = 3, StartColumn = 1, EndColumn = 1 },
                RefactoringTargets = targets ?? new[]
                {
                    new RefactoringTargetModel { Category = "complex-method", Line = 5 },
                },
            };
        }

        private static AgentOutput CreateAgentOutput(string taskId = "test-task-id")
        {
            return new AgentOutput
            {
                TaskId = taskId,
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
                        Replacements = new[]
                        {
                            new AgentReplacement
                            {
                                Search = "old code",
                                Replace = "new code",
                            },
                        },
                    },
                },
            };
        }

        private static void AssertSameFields(JObject expected, JObject actual, params string[] names)
        {
            foreach (var name in names)
            {
                Assert.IsTrue(JToken.DeepEquals(expected[name], actual[name]), name + " mismatch");
            }
        }

        private FakeTelemetryEvent SingleEvent(string name)
        {
            var events = _telemetry.Events.Where(e => e.Name == name).ToList();
            Assert.AreEqual(1, events.Count, $"Expected 1 {name} event, got {events.Count}");
            return events[0];
        }

        private void AssertFailedWith(string error)
        {
            Assert.AreEqual(RefactoringSessionState.Failed, _orchestrator.Current?.State);
            Assert.AreEqual(error, _orchestrator.Current.Error);
        }

        private void AssertCancelledWithoutUpdates(RefactoringSession session)
        {
            Assert.AreEqual(RefactoringSessionState.Cancelled, session.State);
            Assert.AreEqual(0, _host.UpdateReviewCount);
            Assert.IsNull(session.Response ?? (object)session.Error);
        }
    }
}
