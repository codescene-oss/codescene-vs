// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Cli;

public interface ICliServices
{
    /// <summary>
    /// Gets the CLI command provider.
    /// </summary>
    ICliCommandProvider CommandProvider { get; }

    /// <summary>
    /// Gets the process executor for running CLI commands.
    /// </summary>
    IProcessExecutor ProcessExecutor { get; }

    /// <summary>
    /// Gets the cache storage service for CLI operations.
    /// </summary>
    ICacheStorageService CacheStorage { get; }
}
