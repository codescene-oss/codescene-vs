using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    [Export(typeof(ICliFileChecker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliFileChecker : ICliFileChecker
    {
        [Import]
        private readonly ILogger _logger;

        [Import]
        private readonly ICliExecutor _cliExecuter;

        [Import]
        private readonly ICliSettingsProvider _cliSettingsProvider;

        [Import]
        private readonly ICliDownloader _cliDownloader;

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

                var currentCliVersion = await _cliExecuter.GetFileVersionAsync();
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
