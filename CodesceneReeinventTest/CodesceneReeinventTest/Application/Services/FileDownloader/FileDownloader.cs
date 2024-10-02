using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace CodesceneReeinventTest.Application.Services.FileDownloader
{
    public class FileDownloader : IFileDownloader
    {
        private ArtifactInfo _artifactInfo;
        public FileDownloader()
        {
            _artifactInfo = new ArtifactInfo();
        }
        public async Task HandleAsync()
        {
            try
            {
                if (!File.Exists(_artifactInfo.AbsoluteBinaryPath))
                {
                    await DownloadAsync();
                    UnzipFile();
                    RenameFile();
                    DeleteFile();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error while handling extension file:" + ex);
            }
        }
        private async Task DownloadAsync()
        {
            var url = $"https://downloads.codescene.io/enterprise/cli/{_artifactInfo.ArtifactName}";
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    byte[] fileBytes = await client.GetByteArrayAsync(url);

                    string directoryPath = Path.GetDirectoryName(_artifactInfo.ExtensionPath);
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                    }
                    File.WriteAllBytes(_artifactInfo.AbsoluteDownloadPath, fileBytes);

                    Console.WriteLine($"File downloaded to {_artifactInfo.ExtensionPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error downloading file: {ex.Message}");
                }
            }

        }

        private void UnzipFile()
        {
            if (File.Exists(_artifactInfo.AbsoluteDownloadPath))
            {
                if (!Directory.Exists(_artifactInfo.ExtensionPath))
                {
                    Directory.CreateDirectory(_artifactInfo.ExtensionPath);
                }
                ZipFile.ExtractToDirectory(_artifactInfo.AbsoluteDownloadPath, _artifactInfo.ExtensionPath);
                Console.WriteLine($"File extracted to {_artifactInfo.ExtensionPath}");
            }
            else
            {
                Console.WriteLine($"The file {_artifactInfo.AbsoluteDownloadPath} was not found.");
            }
        }
        private void RenameFile()
        {
            if (File.Exists(_artifactInfo.ExecFromZipPath))
            {
                if (!File.Exists(_artifactInfo.AbsoluteBinaryPath))
                {
                    File.Move(_artifactInfo.ExecFromZipPath, _artifactInfo.AbsoluteBinaryPath);
                }
            }
            else
            {
                Console.WriteLine($"The file {_artifactInfo.ExecFromZipPath} was not found.");
            }
        }
        private void DeleteFile()
        {
            if (File.Exists(_artifactInfo.AbsoluteDownloadPath))
            {
                File.Delete(_artifactInfo.AbsoluteDownloadPath);
            }
            else
            {
                Console.WriteLine($"The file at {_artifactInfo.AbsoluteDownloadPath} was not found.");
            }
        }
    }
}
