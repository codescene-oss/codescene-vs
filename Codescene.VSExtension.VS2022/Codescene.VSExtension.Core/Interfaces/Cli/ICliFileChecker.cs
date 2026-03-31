// Copyright (c) CodeScene. All rights reserved.

using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface ICliFileChecker
    {
        Task<bool> CheckAsync(CancellationToken cancellationToken = default);
    }
}
