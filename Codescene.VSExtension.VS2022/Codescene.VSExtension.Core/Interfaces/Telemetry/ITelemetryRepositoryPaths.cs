// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Telemetry
{
    public interface ITelemetryRepositoryPaths
    {
        Task<IReadOnlyList<string>> GetWorkspaceRepositoryPathsAsync();
    }
}
