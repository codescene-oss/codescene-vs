// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces.Extension;

namespace Codescene.VSExtension.VS2022.Options
{
    [Export(typeof(ISettingsProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class GeneralSettingsProvider : ISettingsProvider
    {
        public bool ShowDebugLogs => General.Instance.ShowDebugLogs;

        public string AuthToken => General.Instance.AuthToken;

        public int ServerWorkerThreads => General.Instance.ServerWorkerThreads;

        public bool EnableAutomaticAnalysis => General.Instance.EnableAutomaticAnalysis;

        public bool EnableTelemetry => General.Instance.EnableTelemetry;

        public bool EnableRefactoringAgent => General.Instance.EnableRefactoringAgent;

        public string AgentModel => General.Instance.AgentModel;

        public string AgentModelOverride => General.Instance.AgentModelOverride;

        public string BedrockProfile => General.Instance.BedrockProfile;

        public string BedrockRegion => General.Instance.BedrockRegion;

        public string AnthropicApiKey => General.Instance.AnthropicApiKey;

        public string OpenAIApiKey => General.Instance.OpenAIApiKey;

        public string GoogleApiKey => General.Instance.GoogleApiKey;
    }
}
