// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Codescene.VSExtension.Core.Interfaces.Cli;

[assembly: InternalsVisibleTo("Codescene.VSExtension.Core.Tests")]
[assembly: InternalsVisibleTo("Codescene.VSExtension.Core.IntegrationTests")]

namespace Codescene.VSExtension.Core.Application.Cli;

/// <summary>
/// Facade service that encapsulates CLI-related dependencies to reduce constructor over-injection.
/// </summary>
[Export(typeof(ICliServices))]
[PartCreationPolicy(CreationPolicy.Shared)]
[SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1009:Closing parenthesis should not be followed by a space", Justification = "Space is required by C# syntax for primary constructors with inheritance")]
[method: ImportingConstructor]
internal class CliServices(
    ICliCommandProvider commandProvider,
    IProcessExecutor processExecutor,
    ICacheStorageService cacheStorage) : ICliServices
{
    private readonly ICliCommandProvider _commandProvider = commandProvider ?? throw new System.ArgumentNullException(nameof(commandProvider));
    private readonly IProcessExecutor _processExecutor = processExecutor ?? throw new System.ArgumentNullException(nameof(processExecutor));
    private readonly ICacheStorageService _cacheStorage = cacheStorage ?? throw new System.ArgumentNullException(nameof(cacheStorage));

    public ICliCommandProvider CommandProvider => _commandProvider;

    public IProcessExecutor ProcessExecutor => _processExecutor;

    public ICacheStorageService CacheStorage => _cacheStorage;
}

