// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IProcessExecutor
    {
        Task<string> ExecuteAsync(string arguments, string content = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default);
    }
}
