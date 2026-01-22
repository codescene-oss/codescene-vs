using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
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
        private readonly ICliDownloader _cliDownloader;

        [ImportingConstructor]
        public CliFileChecker(
            ILogger logger,
            ICliExecutor cliExecuter,
            ICliSettingsProvider cliSettingsProvider,
            ICliDownloader cliDownloader)
        {
            _logger = logger;
            _cliExecuter = cliExecuter;
            _cliSettingsProvider = cliSettingsProvider;
            _cliDownloader = cliDownloader;
        }

        public async Task Check()
        {
            try
            {
                if (!File.Exists(_cliSettingsProvider.CliFileFullPath))
                {
                    _logger.Info($"Setting up CodeScene...");
                    var stopwatch = Stopwatch.StartNew();

                    await _cliDownloader.DownloadAsync();
                    stopwatch.Stop();

                    _logger.Info($"CodeScene setup completed in {stopwatch.ElapsedMilliseconds} ms.");
                    return;
                }

                var currentCliVersion = _cliExecuter.GetFileVersion();
                if (currentCliVersion == _cliSettingsProvider.RequiredDevToolVersion)
                {
                    return;
                }

                _logger.Info("Updating CodeScene tool to the latest version...");
                File.Delete(_cliSettingsProvider.CliFileFullPath);
                await _cliDownloader.DownloadAsync();
                _logger.Info($"CodeScene tool updated.");
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to set up the required CodeScene tools.", ex);
            }
        }
    }
}
