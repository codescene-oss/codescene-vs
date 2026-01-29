using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ICliFileChecker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliFileChecker : ICliFileChecker
    {
        private readonly ILogger _logger;
        private readonly ICliExecutor _cliExecuter;
        private readonly ICliSettingsProvider _cliSettingsProvider;

        [ImportingConstructor]
        public CliFileChecker(
            ILogger logger,
            ICliExecutor cliExecuter,
            ICliSettingsProvider cliSettingsProvider)
        {
            _logger = logger;
            _cliExecuter = cliExecuter;
            _cliSettingsProvider = cliSettingsProvider;
        }

        public Task Check()
        {
            try
            {
                if (!File.Exists(_cliSettingsProvider.CliFileFullPath))
                {
                    _logger.Error($"CodeScene CLI file not found at {_cliSettingsProvider.CliFileFullPath}. The CLI should be bundled with the extension.", new FileNotFoundException($"CLI file not found at {_cliSettingsProvider.CliFileFullPath}"));
                    return Task.CompletedTask;
                }

                var currentCliVersion = _cliExecuter.GetFileVersion();
                if (string.IsNullOrEmpty(currentCliVersion))
                {
                    _logger.Warn("Could not determine CLI version. The CLI file exists but version check failed.");
                    return Task.CompletedTask;
                }

                _logger.Debug($"Using CLI version: {currentCliVersion}");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to check the CodeScene CLI file.", ex);
                return Task.CompletedTask;
            }
        }
    }
}
