// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Agent;

namespace Codescene.VSExtension.Core.Interfaces.Agent
{
    public interface IAgentConnectivityTester
    {
        Task<ConnectivityResult> TestAsync(string workDir, CancellationToken cancellationToken);
    }
}
