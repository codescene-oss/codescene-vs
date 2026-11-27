// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces.Agent;

namespace Codescene.VSExtension.Core.Application.Agent
{
    [Export(typeof(IAgentProcessTracker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal class AgentProcessTracker : IAgentProcessTracker
    {
        private readonly object _lock = new object();
        private readonly List<IAgentProcess> _processes = new List<IAgentProcess>();

        public void Track(IAgentProcess process)
        {
            if (process == null)
            {
                return;
            }

            lock (_lock)
            {
                _processes.Add(process);

                process.WaitForExitAsync().ContinueWith(
                    _ =>
                    {
                        lock (_lock)
                        {
                            _processes.Remove(process);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        public void KillAll()
        {
            IAgentProcess[] processes;

            lock (_lock)
            {
                processes = _processes.ToArray();
                _processes.Clear();
            }

            foreach (var process in processes)
            {
                try
                {
                    process.KillTree();
                    process.Dispose();
                }
                catch
                {
                }
            }
        }
    }
}
