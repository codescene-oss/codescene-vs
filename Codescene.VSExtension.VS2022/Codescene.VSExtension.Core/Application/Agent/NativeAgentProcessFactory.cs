// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Models.Agent;

namespace Codescene.VSExtension.Core.Application.Agent
{
    [Export(typeof(IAgentProcessFactory))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class NativeAgentProcessFactory : IAgentProcessFactory
    {
        private readonly ILogger _logger;

        [ImportingConstructor]
        public NativeAgentProcessFactory(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IAgentProcess Start(AgentProcessStartInfo startInfo)
        {
            if (startInfo == null)
            {
                throw new ArgumentNullException(nameof(startInfo));
            }

            var psi = new ProcessStartInfo
            {
                FileName = startInfo.FileName,
                Arguments = startInfo.Arguments,
                WorkingDirectory = startInfo.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            };

            if (startInfo.EnvironmentAdditions != null)
            {
                foreach (var kvp in startInfo.EnvironmentAdditions)
                {
                    psi.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }

            var process = Process.Start(psi);
            return new NativeAgentProcess(process, _logger);
        }
    }
}
