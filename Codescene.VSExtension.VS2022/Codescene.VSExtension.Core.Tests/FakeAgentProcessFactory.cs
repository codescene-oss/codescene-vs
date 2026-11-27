// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Models.Agent;

namespace Codescene.VSExtension.Core.Tests
{
    internal class FakeAgentProcessFactory : IAgentProcessFactory
    {
        private readonly FakeAgentProcess _process;

        public FakeAgentProcessFactory(FakeAgentProcess process)
        {
            _process = process;
        }

        public int StartCount { get; private set; }

        public AgentProcessStartInfo LastStartInfo { get; private set; }

        public IAgentProcess Start(AgentProcessStartInfo startInfo)
        {
            StartCount++;
            LastStartInfo = startInfo;
            _process.StartInfo = startInfo;
            return _process;
        }
    }
}
