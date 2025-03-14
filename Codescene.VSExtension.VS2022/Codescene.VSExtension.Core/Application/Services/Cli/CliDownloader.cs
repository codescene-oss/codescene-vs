using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.Cli
{
    public class CliDownloader : ICliDownloader
    {
        private readonly ILogger _logger;
        private readonly ICliSettingsProvider _cliSettingsProvider;
        private readonly string _workingDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        private string DownloadedArtifactFilePath => Path.Combine(_workingDirectory, _cliSettingsProvider.CliArtifactName);

        public CliDownloader(ILogger logger, ICliSettingsProvider cliSettingsProvider)
        {
            _logger = logger;
            _cliSettingsProvider = cliSettingsProvider;
        }

        public async Task DownloadAsync()
        {
            DeleteArtifactFile();
            await DownloadArtifactFileAsync();
            UnzipArtifactFile();
            DeleteArtifactFile();
        }

        /// <summary>
        /// Download artifact zipped file from the Codescene repository
        /// </summary>
        /// <returns></returns>
        private async Task DownloadArtifactFileAsync()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var fileBytes = await client.GetByteArrayAsync(_cliSettingsProvider.CliArtifactUrl);

                    var directoryPath = Path.GetDirectoryName(_workingDirectory);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    File.WriteAllBytes(DownloadedArtifactFilePath, fileBytes);

                    _logger.Info($"File downloaded to {_workingDirectory}");
                }
                catch (Exception ex)
                {
                    _logger.Error("Error downloading artifact file", ex);
                }
            }
        }
        private void UnzipArtifactFile()
        {
            var tempFolder = Path.Combine(_workingDirectory, "UnzippedArtifact");
            if (File.Exists(DownloadedArtifactFilePath))
            {
                if (!Directory.Exists(_workingDirectory))
                {
                    Directory.CreateDirectory(_workingDirectory);
                }
                ZipFile.ExtractToDirectory(DownloadedArtifactFilePath, tempFolder);
                var cliFilePathInUnzippedFolder = Path.Combine(tempFolder, _cliSettingsProvider.CliFileName);
                if (!File.Exists(cliFilePathInUnzippedFolder))
                {
                    throw new Exception($"In the artifact archive doesn't exits cli file with name:{_cliSettingsProvider.CliFileName}");
                }

                File.Copy(cliFilePathInUnzippedFolder, _cliSettingsProvider.CliFileFullPath, true);
                Directory.Delete(tempFolder, true);
                _logger.Info($"File extracted to {_workingDirectory}");
                return;
            }

            _logger.Info($"The file {DownloadedArtifactFilePath} was not found.");
        }

        private void DeleteArtifactFile()
        {
            if (File.Exists(DownloadedArtifactFilePath))
            {
                File.Delete(DownloadedArtifactFilePath);
                return;
            }

            _logger.Info($"The file at {DownloadedArtifactFilePath} was not found.");
        }
    }
}