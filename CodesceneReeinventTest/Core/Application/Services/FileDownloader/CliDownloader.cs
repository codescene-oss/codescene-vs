using Core.Application.Services.ErrorHandling;
using Core.Application.Services.FileReviewer;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace Core.Application.Services.FileDownloader
{
    public class CliDownloader : ICliDownloader
    {
        private readonly IErrorsHandler _errorsHandler;
        private readonly ICliExecuter _cliExecuter;
        public CliDownloader(IErrorsHandler errorsHandler, ICliExecuter cliExecuter)
        {
            _errorsHandler = errorsHandler;
            _cliExecuter = cliExecuter;
        }

        private async Task DownloadAsync()
        {
            await DownloadFromRepoAsync();
            UnzipFile();
            RenameFile();
            DeleteFile();
        }
        private async Task DownloadFromRepoAsync()
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var fileBytes = await client.GetByteArrayAsync(ArtifactInfo.ArtifactURL);

                    var directoryPath = Path.GetDirectoryName(ArtifactInfo.ExtensionPath);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    File.WriteAllBytes(ArtifactInfo.AbsoluteDownloadPath, fileBytes);

                    Console.WriteLine($"File downloaded to {ArtifactInfo.ExtensionPath}");
                }
                catch (Exception ex)
                {
                    var message = "Error downloading cli file";
                    _errorsHandler.Log(message, ex);
                    Console.WriteLine($"{message}: {ex.Message}");
                }
            }
        }
        private void UnzipFile()
        {
            if (File.Exists(ArtifactInfo.AbsoluteDownloadPath))
            {
                if (!Directory.Exists(ArtifactInfo.ExtensionPath))
                {
                    Directory.CreateDirectory(ArtifactInfo.ExtensionPath);
                }
                ZipFile.ExtractToDirectory(ArtifactInfo.AbsoluteDownloadPath, ArtifactInfo.ExtensionPath);
                Console.WriteLine($"File extracted to {ArtifactInfo.ExtensionPath}");
                return;
            }

            Console.WriteLine($"The file {ArtifactInfo.AbsoluteDownloadPath} was not found.");
        }

        private void RenameFile()
        {
            if (File.Exists(ArtifactInfo.ExecFromZipPath))
            {
                if (!File.Exists(ArtifactInfo.ABSOLUTE_CLI_FILE_PATH))
                {
                    File.Move(ArtifactInfo.ExecFromZipPath, ArtifactInfo.ABSOLUTE_CLI_FILE_PATH);
                }
                return;
            }

            Console.WriteLine($"The file {ArtifactInfo.ExecFromZipPath} was not found.");
        }

        private void DeleteFile()
        {
            if (File.Exists(ArtifactInfo.AbsoluteDownloadPath))
            {
                File.Delete(ArtifactInfo.AbsoluteDownloadPath);
                return;
            }

            Console.WriteLine($"The file at {ArtifactInfo.AbsoluteDownloadPath} was not found.");
        }

        public async Task DownloadOrUpgradeAsync()
        {
            try
            {
                if (!File.Exists(ArtifactInfo.ABSOLUTE_CLI_FILE_PATH))
                {
                    await DownloadAsync();
                    return;
                }

                var currentCliVersion = _cliExecuter.GetFileVersion();
                if (currentCliVersion == ArtifactInfo.REQUIRED_DEV_TOOLS_VERSION)
                {
                    return;
                }

                File.Delete(ArtifactInfo.ABSOLUTE_CLI_FILE_PATH);
                await DownloadAsync();
            }
            catch (Exception ex)
            {
                var message = "Error downloading or upgrading CLI file";
                _errorsHandler.Log(message, ex);
                Console.WriteLine($"{message}: {ex.Message}");
            }
        }
    }
}