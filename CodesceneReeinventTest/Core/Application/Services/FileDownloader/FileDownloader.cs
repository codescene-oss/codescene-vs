using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace Core.Application.Services.FileDownloader
{
    public class FileDownloader : IFileDownloader
    {
        public async Task HandleAsync()
        {
            try
            {
                if (!File.Exists(ArtifactInfo.AbsoluteBinaryPath))
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
                    Console.WriteLine($"Error downloading file: {ex.Message}");
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
                if (!File.Exists(ArtifactInfo.AbsoluteBinaryPath))
                {
                    File.Move(ArtifactInfo.ExecFromZipPath, ArtifactInfo.AbsoluteBinaryPath);
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
        private void Test3(string v, string c, string d, string e, string f)
        {
            if (1 == 2 || 2 == 2 || 3 == 2)
            {

            }
        }
    }
}