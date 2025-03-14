using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class CliDownloaderTests
    {
        // Fake implementation of ILogger that records log messages.
        private class FakeLogger : ILogger
        {
            public readonly System.Collections.Generic.List<string> InfoMessages = new System.Collections.Generic.List<string>();
            public readonly System.Collections.Generic.List<(string Message, Exception Ex)> ErrorMessages = new System.Collections.Generic.List<(string, Exception)>();

            public void Info(string message)
            {
                InfoMessages.Add(message);
            }

            public void Error(string message, Exception ex)
            {
                ErrorMessages.Add((message, ex));
            }

            public Task LogAsync(string message, Exception ex)
            {
                throw new NotImplementedException();
            }
        }

        // Fake implementation of ICliSettingsProvider allowing us to override settings.
        private class FakeCliSettingsProvider : ICliSettingsProvider
        {
            public string RequiredDevToolVersion { get; set; }
            public string CliArtifactName { get; set; }
            public string CliArtifactUrl { get; set; }
            public string CliFileName { get; set; }
            public string ArtifactBaseUrl { get; set; }
            public string CliFileFullPath { get; set; }
        }

        [TestMethod]
        public async Task DownloadAsync_ValidZipFile_ShouldDownloadAndExtractCliFile()
        {
            // ARRANGE

            // Create a temporary directory to hold the final extracted CLI file.
            string tempDestDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDestDir);
            string destFilePath = Path.Combine(tempDestDir, "cs-ide.exe");

            // Set up a fake settings provider.
            var fakeSettings = new FakeCliSettingsProvider
            {
                RequiredDevToolVersion = "3b28b97d2f4a17d596c6f2ec5cf2e86363c08d21",
                // Use a simple artifact name for testing.
                CliArtifactName = "artifact.zip",
                // CliArtifactUrl will be set after finding a free port.
                CliArtifactUrl = string.Empty,
                CliFileName = "cs-ide.exe",
                ArtifactBaseUrl = "http://dummy", // Not used in this test.
                CliFileFullPath = destFilePath
            };

            // Set up a fake logger.
            var fakeLogger = new FakeLogger();

            // Create a zip file in memory containing an entry "cs-ide.exe" with the text "Hello CLI".
            byte[] zipBytes;
            using (var ms = new MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    var entry = archive.CreateEntry("cs-ide.exe");
                    using (var entryStream = entry.Open())
                    using (var writer = new StreamWriter(entryStream))
                    {
                        writer.Write("Hello CLI");
                    }
                }
                zipBytes = ms.ToArray();
            }

            // Find a free port for the HTTP listener.
            int port;
            var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();

            // Update the fake settings with the artifact URL pointing to our local HTTP server.
            fakeSettings.CliArtifactUrl = $"http://localhost:{port}/artifact.zip";

            // Start an HTTP listener to serve the zip file.
            var httpListener = new HttpListener();
            httpListener.Prefixes.Add($"http://localhost:{port}/");
            httpListener.Start();

            // Run the HTTP listener in a background task.
            var listenerTask = Task.Run(async () =>
            {
                var context = await httpListener.GetContextAsync();
                if (context.Request.RawUrl.Equals("/artifact.zip", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.ContentType = "application/zip";
                    context.Response.ContentLength64 = zipBytes.Length;
                    await context.Response.OutputStream.WriteAsync(zipBytes, 0, zipBytes.Length);
                }
                context.Response.Close();
                httpListener.Stop();
            });

            // Instantiate the CliDownloader with our fake dependencies.
            var cliDownloader = new CliDownloader(fakeLogger, fakeSettings);

            // ACT
            await cliDownloader.DownloadAsync();
            await listenerTask; // Ensure the HTTP listener task has completed.

            // ASSERT

            // Check that the destination CLI file exists and contains the expected content.
            Assert.IsTrue(File.Exists(destFilePath), "The CLI file should exist after extraction.");
            string extractedContent = File.ReadAllText(destFilePath);
            Assert.AreEqual("Hello CLI", extractedContent, "The extracted CLI file content should match the expected value.");

            // Verify that the downloaded artifact file is deleted.
            // The downloaded file is located in the working directory of the executing assembly.
            string workingDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string downloadedArtifactFilePath = Path.Combine(workingDir, fakeSettings.CliArtifactName);
            Assert.IsFalse(File.Exists(downloadedArtifactFilePath), "The downloaded artifact file should be deleted after extraction.");

            // Cleanup: Remove the temporary destination directory.
            if (Directory.Exists(tempDestDir))
            {
                Directory.Delete(tempDestDir, true);
            }
        }
    }
}
