// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Moq;

namespace Codescene.VSExtension.Core.IntegrationTests.TestImplementations
{
    [Export(typeof(ISettingsProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class TestSettingsProvider : ISettingsProvider
    {
        internal Mock<ISettingsProvider> Mock = new Mock<ISettingsProvider>();

        public bool ShowDebugLogs => Mock.Object.ShowDebugLogs;

        public string AuthToken => Mock.Object.AuthToken;

        public int ServerWorkerThreads => Mock.Object.ServerWorkerThreads;

        public bool EnableAutomaticAnalysis => true;

        public bool EnableTelemetry => true;

        public bool EnableRefactoringAgent => true;

        public string AgentModel => "anthropic/claude-sonnet-5-5";

        public string AgentModelOverride => string.Empty;

        public string BedrockProfile => string.Empty;

        public string BedrockRegion => string.Empty;

        public string AnthropicApiKey => string.Empty;

        public string OpenAIApiKey => string.Empty;

        public string GoogleApiKey => string.Empty;
    }
}
