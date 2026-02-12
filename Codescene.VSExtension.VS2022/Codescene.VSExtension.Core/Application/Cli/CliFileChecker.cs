// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.IO;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ICliFileChecker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliFileChecker : ICliFileChecker
    {
        private readonly ILogger _logger;
        private readonly ICliExecutor _cliExecutor;
        private readonly ICliSettingsProvider _cliSettingsProvider;

        [ImportingConstructor]
        public CliFileChecker(
            ILogger logger,
            ICliExecutor cliExecutor,
            ICliSettingsProvider cliSettingsProvider)
        {
            _logger = logger;
            _cliExecutor = cliExecutor;
            _cliSettingsProvider = cliSettingsProvider;
        }

        public bool Check()
        {
            try
            {
                if (!File.Exists(_cliSettingsProvider.CliFileFullPath))
                {
                    _logger.Error($"CodeScene CLI file not found at {_cliSettingsProvider.CliFileFullPath}. The CLI should be bundled with the extension.", new FileNotFoundException($"CLI file not found at {_cliSettingsProvider.CliFileFullPath}"));
                    return false;
                }

                var currentCliVersion = _cliExecutor.GetFileVersion();
                if (string.IsNullOrEmpty(currentCliVersion))
                {
                    _logger.Warn("Could not determine CLI version. The CLI file exists but version check failed.");
                    return false;
                }

                _logger.Debug($"Using CLI version: {currentCliVersion}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to check the CodeScene CLI file.", ex);
                return false;
            }
        }
    }
}
