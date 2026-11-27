// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Agent
{
    public interface IAgentProcess : IDisposable
    {
        event EventHandler<string> StdoutLine;

        event EventHandler<string> StderrLine;

        Task<int> WaitForExitAsync();

        void KillTree();
    }
}
