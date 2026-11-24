// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces.Agent;
using Codescene.VSExtension.Core.Models.Agent;

namespace Codescene.VSExtension.Core.Tests
{
    internal class FakeAgentProcess : IAgentProcess
    {
        private readonly IReadOnlyList<string> _stdoutLines;
        private readonly IReadOnlyList<string> _stderrLines;
        private readonly int _exitCode;
        private readonly bool _exitsOnItsOwn;
        private readonly TaskCompletionSource<int> _exitTcs = new TaskCompletionSource<int>();
        private readonly object _lock = new object();
        private bool _ran;

        public FakeAgentProcess(List<string> stdoutLines, List<string> stderrLines, int exitCode, bool exitsOnItsOwn = true)
        {
            _stdoutLines = stdoutLines ?? new List<string>();
            _stderrLines = stderrLines ?? new List<string>();
            _exitCode = exitCode;
            _exitsOnItsOwn = exitsOnItsOwn;
        }

        public event EventHandler<string> StdoutLine;

        public event EventHandler<string> StderrLine;

        public Action<AgentProcessStartInfo> BeforeExit { get; set; }

        public AgentProcessStartInfo StartInfo { get; set; }

        public bool WasKilled { get; private set; }

        public bool HasExited => _exitTcs.Task.IsCompleted;

        public void Start()
        {
            if (_exitsOnItsOwn)
            {
                Run();
            }
        }

        public Task<int> WaitForExitAsync()
        {
            Start();
            return _exitTcs.Task;
        }

        public void KillTree()
        {
            if (HasExited)
            {
                return;
            }

            WasKilled = true;
            _exitTcs.TrySetResult(-1);
        }

        public void Dispose()
        {
        }

        private void Run()
        {
            lock (_lock)
            {
                if (_ran)
                {
                    return;
                }

                _ran = true;
            }

            BeforeExit?.Invoke(StartInfo);

            foreach (var line in _stdoutLines)
            {
                StdoutLine?.Invoke(this, line);
            }

            foreach (var line in _stderrLines)
            {
                StderrLine?.Invoke(this, line);
            }

            _exitTcs.TrySetResult(_exitCode);
        }
    }
}
