// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ICliFileChecker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliFileChecker : ICliFileChecker
    {
        private readonly ILogger _logger;
        private readonly ICliSettingsProvider _cliSettingsProvider;

        [ImportingConstructor]
        public CliFileChecker(ILogger logger, ICliSettingsProvider cliSettingsProvider)
        {
            _logger = logger;
            _cliSettingsProvider = cliSettingsProvider;
        }

        public Task<bool> CheckAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!File.Exists(_cliSettingsProvider.JavaExeFullPath) || !File.Exists(_cliSettingsProvider.JarFullPath))
                {
                    _logger.Error(
                        $"CodeScene IDE server distribution not found at {_cliSettingsProvider.DistributionFullPath}. The CLI should be bundled with the extension.",
                        new FileNotFoundException($"IDE server distribution not found at {_cliSettingsProvider.DistributionFullPath}"));
                    return Task.FromResult(false);
                }

                _logger.Debug($"Using IDE server distribution at {_cliSettingsProvider.DistributionFullPath}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to check the CodeScene IDE server distribution.", ex);
                return Task.FromResult(false);
            }
        }
    }
}
