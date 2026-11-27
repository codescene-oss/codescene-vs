// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Application.Refactoring;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Refactoring;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Refactoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class RefactoringSessionStaleTests
    {
        private Mock<IRefactoringHost> _host;
        private Mock<IAgentSkillRunner> _runner;
        private Mock<IAgentAvailability> _availability;
        private Mock<IProviderKeyStore> _keyStore;
        private Mock<ISettingsProvider> _settings;
        private Mock<ITelemetryManager> _telemetry;
        private FakeAsyncTaskScheduler _scheduler;
        private AceComponentMapper _mapper;
        private Mock<ILogger> _logger;
        private RefactoringOrchestrator _orchestrator;

        [TestInitialize]
        public void Setup()
        {
            _host = new Mock<IRefactoringHost>();
            _runner = new Mock<IAgentSkillRunner>();
            _availability = new Mock<IAgentAvailability>();
            _keyStore = new Mock<IProviderKeyStore>();
            _settings = new Mock<ISettingsProvider>();
            _telemetry = new Mock<ITelemetryManager>();
            _scheduler = new FakeAsyncTaskScheduler();
            _mapper = new AceComponentMapper();
            _logger = new Mock<ILogger>();

            _availability.SetupGet(x => x.Usable).Returns(true);
            _keyStore.Setup(x => x.GetAll()).Returns(new System.Collections.Generic.Dictionary<string, string> { { "anthropic", "key" } });
            _settings.SetupGet(x => x.AgentModel).Returns("anthropic/claude-sonnet-5-5");

            _orchestrator = new RefactoringOrchestrator(
                _host.Object,
                _runner.Object,
                _availability.Object,
                _keyStore.Object,
                _settings.Object,
                _telemetry.Object,
                _scheduler,
                _mapper,
                _logger.Object);
        }

        [TestMethod]
        public async Task MarkStale_SetsStateAndView()
        {
            var fn = CreateFnToRefactor();
            await SetupPresentedSession(fn);

            _orchestrator.MarkStale();
            await _scheduler.WaitForAllAsync();

            Assert.IsNotNull(_orchestrator.Current);
            Assert.AreEqual(RefactoringSessionState.Stale, _orchestrator.Current.State);
            _host.Verify(x => x.UpdateReviewAsync(It.Is<Models.WebComponent.Data.AceComponentData>(d => d.IsStale)), Times.Once);
        }

        [TestMethod]
        public void MarkStale_NoSession_NoOp()
        {
            _orchestrator.MarkStale();

            _host.Verify(x => x.UpdateReviewAsync(It.IsAny<Models.WebComponent.Data.AceComponentData>()), Times.Never);
        }

        [TestMethod]
        public async Task UpdateRange_MovesFunction()
        {
            var fn = CreateFnToRefactor("TestFunction");
            await SetupPresentedSession(fn);

            var newRange = new CliRangeModel
            {
                StartLine = 20,
                EndLine = 30,
                StartColumn = 5,
                EndColumn = 10,
            };

            _orchestrator.UpdateRange(newRange);

            Assert.IsNotNull(_orchestrator.Current);
            Assert.AreEqual(20, _orchestrator.Current.Function.Range.StartLine);
            Assert.AreEqual(30, _orchestrator.Current.Function.Range.EndLine);
            Assert.AreEqual(5, _orchestrator.Current.Function.Range.StartColumn);
            Assert.AreEqual(10, _orchestrator.Current.Function.Range.EndColumn);
            Assert.AreEqual("TestFunction", _orchestrator.Current.Function.Name);
        }

        private static FnToRefactorModel CreateFnToRefactor(string name = "TestFunction")
        {
            return new FnToRefactorModel
            {
                Name = name,
                Body = "function body",
                FileType = "cs",
                FunctionType = "cs",
                Range = new CliRangeModel { StartLine = 10, EndLine = 20, StartColumn = 1, EndColumn = 1 },
                RefactoringTargets = new[]
                {
                    new RefactoringTargetModel { Category = "complex-method", Line = 15 },
                },
            };
        }

        private async Task SetupPresentedSession(FnToRefactorModel fn)
        {
            _host.Setup(x => x.IsDirty(It.IsAny<string>())).Returns(false);
            _host.Setup(x => x.ReadDocumentText(It.IsAny<string>())).Returns("function body");
            _host.Setup(x => x.ShowReviewAsync(It.IsAny<Models.WebComponent.Data.AceComponentData>())).Returns(Task.CompletedTask);
            _host.Setup(x => x.UpdateReviewAsync(It.IsAny<Models.WebComponent.Data.AceComponentData>())).Returns(Task.CompletedTask);

            _runner.Setup(x => x.RunAsync<Models.Agent.AgentOutput>(
                It.IsAny<Models.Agent.AgentSkill>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Action<string>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Models.Agent.AgentOutput
                {
                    TaskId = "task-123",
                    FixResult = "fix_proposed",
                    Confidence = "high",
                    Summary = "Refactored",
                    Reasoning = "Test",
                    Changes = new Models.Agent.AgentChange[0],
                });

            await _orchestrator.RequestAsync("codeaction", "C:/test/file.cs", fn);
            await _scheduler.WaitForAllAsync();

            var session = _orchestrator.Current;
            Assert.IsNotNull(session);
            Assert.AreEqual(RefactoringSessionState.Presented, session.State);
        }
    }
}
