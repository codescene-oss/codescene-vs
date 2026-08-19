// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli.Rpc;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IIdeServerHost : IDisposable
    {
        event EventHandler Restarted;

        IIdeServerClient Client { get; }

        ServerStartMetadata Metadata { get; }

        bool IsRunning { get; }

        Task StartAsync(CancellationToken cancellationToken = default);

        Task RestartAsync(CancellationToken cancellationToken = default);
    }
}
