using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public class CliFileChecker : ICliFileChecker
    {
        private readonly ILogger _logger;
        private readonly ICliExecuter _cliExecuter;
        private readonly ICliSettingsProvider _cliSettingsProvider;
        private readonly ICliDownloader _cliDownloader;
        public CliFileChecker(ILogger logger, ICliSettingsProvider cliSettingsProvider, ICliExecuter cliExecuter, ICliDownloader cliDownloader)
        {
            _cliSettingsProvider = cliSettingsProvider;
            _cliExecuter = cliExecuter;
            _cliDownloader = cliDownloader;
            _logger = logger;
        }

        public async Task Check()
        {
            try
            {
                if (!File.Exists(_cliSettingsProvider.CliFileFullPath))
                {
                    _logger.Info($"Cli file doesn't exist. Downloading file...");
                    await _cliDownloader.DownloadAsync();
                    _logger.Info($"Downloaded cli file.");
                    return;
                }

                var currentCliVersion = _cliExecuter.GetFileVersion();
                if (currentCliVersion == _cliSettingsProvider.RequiredDevToolVersion)
                {
                    _logger.Info($"File with required version:{_cliSettingsProvider.RequiredDevToolVersion} already exists.");
                    return;
                }

                File.Delete(_cliSettingsProvider.CliFileFullPath);
                await _cliDownloader.DownloadAsync();
                _logger.Info($"Downloaded a new version of cli file.");
            }
            catch (Exception ex)
            {
                _logger.Error("Error downloading artifact file", ex);
            }
        }
    }
}
