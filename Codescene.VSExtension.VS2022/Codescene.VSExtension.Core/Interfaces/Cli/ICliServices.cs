// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Cli;

public interface ICliServices
{
    IIdeServerHost Host { get; }

    ICacheStorageService CacheStorage { get; }
}
