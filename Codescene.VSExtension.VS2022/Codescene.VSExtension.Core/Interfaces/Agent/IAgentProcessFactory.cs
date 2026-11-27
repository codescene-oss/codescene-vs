// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Agent;

namespace Codescene.VSExtension.Core.Interfaces.Agent
{
    public interface IAgentProcessFactory
    {
        IAgentProcess Start(AgentProcessStartInfo startInfo);
    }
}
