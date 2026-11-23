// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface ICliExecutor
    {
        Task<string> GetFileVersionAsync(CancellationToken cancellationToken = default);

        Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default);
    }
}
