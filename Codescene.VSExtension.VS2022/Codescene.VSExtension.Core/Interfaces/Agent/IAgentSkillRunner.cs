// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Agent;

namespace Codescene.VSExtension.Core.Interfaces.Agent
{
    public interface IAgentSkillRunner
    {
        Task<TOutput> RunAsync<TOutput>(
            AgentSkill skill,
            string workDir,
            object input,
            Action<string> onProgress,
            CancellationToken cancellationToken)
            where TOutput : class;
    }
}
