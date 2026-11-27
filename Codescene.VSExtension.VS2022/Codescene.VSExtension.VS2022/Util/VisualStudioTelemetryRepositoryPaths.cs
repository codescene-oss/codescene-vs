// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.VS2022.Util;

[Export(typeof(ITelemetryRepositoryPaths))]
[PartCreationPolicy(CreationPolicy.Shared)]
public sealed class VisualStudioTelemetryRepositoryPaths : ITelemetryRepositoryPaths
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(60);

    private readonly object _lock = new object();
    private IReadOnlyList<string> _cached;
    private DateTime _cachedAt = DateTime.MinValue;

    public async Task<IReadOnlyList<string>> GetWorkspaceRepositoryPathsAsync()
    {
        lock (_lock)
        {
            if (_cached != null && DateTime.UtcNow - _cachedAt < CacheDuration)
            {
                return _cached;
            }
        }

        var directories = await WorkspacePathResolver.GetWorkspaceRootsAsync();
        var directoryList = directories?.ToList() ?? new List<string>();
        var roots = directoryList.Count == 0
            ? (IReadOnlyList<string>)Array.Empty<string>()
            : await Task.Run(() => GitPathDiscovery.ResolveRepositoryRoots(directoryList));

        lock (_lock)
        {
            _cached = roots;
            _cachedAt = DateTime.UtcNow;
        }

        return roots;
    }
}
