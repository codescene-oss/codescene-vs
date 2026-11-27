// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Agent
{
    public interface IAgentProcessTracker
    {
        void Track(IAgentProcess process);

        void KillAll();
    }
}
