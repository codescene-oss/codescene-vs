// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Interfaces.Refactoring;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models.Agent;
using Codescene.VSExtension.Core.Models.WebComponent.Data;

namespace Codescene.VSExtension.Core.Tests
{
    internal class FakeRefactoringHost : IRefactoringHost
    {
        public bool IsDirtyResult { get; set; }

        public bool ConfirmSaveResult { get; set; } = true;

        public bool ConfirmSaveCalled { get; private set; }

        public string ConfirmSaveFileName { get; private set; }

        public bool SaveCalled { get; private set; }

        public string SavedPath { get; private set; }

        public bool OfferSetApiKeyCalled { get; private set; }

        public string OfferSetApiKeyMessage { get; private set; }

        public int ShowReviewCount { get; private set; }

        public int UpdateReviewCount { get; private set; }

        public List<AceComponentData> ShownData { get; } = new List<AceComponentData>();

        public List<AceComponentData> UpdatedData { get; } = new List<AceComponentData>();

        public List<string> Calls { get; } = new List<string>();

        public bool IsDirty(string path)
        {
            return IsDirtyResult;
        }

        public Task<bool> ConfirmSaveAsync(string fileName)
        {
            ConfirmSaveCalled = true;
            ConfirmSaveFileName = fileName;
            Calls.Add("confirm-save:" + fileName);
            return Task.FromResult(ConfirmSaveResult);
        }

        public Task SaveAsync(string path)
        {
            SaveCalled = true;
            SavedPath = path;
            Calls.Add("save:" + path);
            return Task.CompletedTask;
        }

        public Task OfferSetApiKeyAsync(string message)
        {
            OfferSetApiKeyCalled = true;
            OfferSetApiKeyMessage = message;
            return Task.CompletedTask;
        }

        public Task ShowReviewAsync(AceComponentData data)
        {
            ShowReviewCount++;
            Calls.Add("show-review");
            ShownData.Add(data);
            return Task.CompletedTask;
        }

        public Task UpdateReviewAsync(AceComponentData data)
        {
            UpdateReviewCount++;
            UpdatedData.Add(data);
            return Task.CompletedTask;
        }

        public string ReadDocumentText(string path)
        {
            return "function body\nold code\nmore code";
        }
    }

    internal class FakeAgentSkillRunner : IAgentSkillRunner
    {
        public bool WasCalled { get; private set; }

        public AgentSkill Skill { get; private set; }

        public string WorkDir { get; private set; }

        public object Input { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public AgentOutput Output { get; set; }

        public Exception ThrowOnRun { get; set; }

        public Action<Action<string>> OnProgress { get; set; }

        public Action OnRunStart { get; set; }

        public bool IgnoresCancellation { get; set; }

        public async Task<TOutput> RunAsync<TOutput>(
            AgentSkill skill,
            string workDir,
            object input,
            Action<string> onProgress,
            CancellationToken cancellationToken)
            where TOutput : class
        {
            OnRunStart?.Invoke();
            WasCalled = true;
            Skill = skill;
            WorkDir = workDir;
            Input = input;
            LastCancellationToken = cancellationToken;

            OnProgress?.Invoke(onProgress);

            if (ThrowOnRun != null)
            {
                throw ThrowOnRun;
            }

            await Task.Delay(10, IgnoresCancellation ? CancellationToken.None : cancellationToken);

            if (Output is AgentOutput agentOutput && input is AgentInput agentInput)
            {
                agentOutput.TaskId = agentInput.TaskId;
            }

            var result = Output as TOutput;
            return result!;
        }
    }

    internal class FakeTelemetryManager : ITelemetryManager
    {
        public List<FakeTelemetryEvent> Events { get; } = new List<FakeTelemetryEvent>();

        public Task SendTelemetryAsync(string eventName, Dictionary<string, object> additionalEventData = null, CancellationToken cancellationToken = default)
        {
            Events.Add(new FakeTelemetryEvent { Name = eventName, Data = additionalEventData ?? new Dictionary<string, object>() });
            return Task.CompletedTask;
        }

        public int HoldRouting() => 0;

        public void RoutingReady(int generation)
        {
        }

        public Task EndSessionAsync(TimeSpan timeout) => Task.CompletedTask;
    }

    internal class FakeTelemetryEvent
    {
        public string Name { get; set; }

        public Dictionary<string, object> Data { get; set; }
    }

    internal class FakeAsyncTaskScheduler : IAsyncTaskScheduler
    {
        private readonly List<Func<CancellationToken, Task>> _scheduled = new List<Func<CancellationToken, Task>>();

        public void Schedule(Func<Task> asyncWork)
        {
            _scheduled.Add(ct => asyncWork());
        }

        public void Schedule(Func<CancellationToken, Task> asyncWork)
        {
            _scheduled.Add(asyncWork);
        }

        public void Schedule(Func<CancellationToken, Task> asyncWork, CancellationToken cancellationToken)
        {
            _scheduled.Add(asyncWork);
        }

        public async Task WaitForAllAsync()
        {
            var tasks = _scheduled.ToList();
            _scheduled.Clear();

            foreach (var task in tasks)
            {
                await task(CancellationToken.None);
            }
        }
    }
}
