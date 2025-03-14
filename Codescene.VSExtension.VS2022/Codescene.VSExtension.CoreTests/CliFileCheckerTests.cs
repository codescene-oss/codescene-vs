using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.ReviewResultModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class CliFileCheckerTests
    {
        // Fake implementation of ILogger to record log messages.
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

        // Fake settings provider that lets us set the CliFileFullPath.
        private class FakeCliSettingsProvider : ICliSettingsProvider
        {
            public string RequiredDevToolVersion => "3b28b97d2f4a17d596c6f2ec5cf2e86363c08d21";
            public string CliArtifactName => $"cs-ide-windows-amd64-{RequiredDevToolVersion}.zip";
            public string CliArtifactUrl => $"{ArtifactBaseUrl}{CliArtifactName}";
            public string CliFileName => "cs-ide.exe";
            public string ArtifactBaseUrl => "https://downloads.codescene.io/enterprise/cli/";
            // Allow file path override for testing
            public string CliFileFullPath { get; set; }
        }

        // Fake CLI executer that returns a version we can control.
        private class FakeCliExecuter : ICliExecuter
        {
            public string VersionToReturn { get; set; }

            public void AddToActiveReviewList(string documentPath)
            {
                throw new NotImplementedException();
            }

            public void AddToActiveReviewList(string documentPath, string content)
            {
                throw new NotImplementedException();
            }

            public string GetFileVersion() => VersionToReturn;

            public ReviewMapModel GetReviewObject(string filePath)
            {
                throw new NotImplementedException();
            }

            public List<ReviewModel> GetTaggerItems(string filePath)
            {
                throw new NotImplementedException();
            }

            public void RemoveFromActiveReviewList(string documentPath)
            {
                throw new NotImplementedException();
            }

            public ReviewMapModel Review(string path)
            {
                throw new NotImplementedException();
            }
        }

        // Fake CLI downloader that records whether DownloadAsync was called.
        private class FakeCliDownloader : ICliDownloader
        {
            public bool DownloadCalled { get; private set; }
            // When set to true, DownloadAsync will throw an exception.
            public bool ThrowException { get; set; }
            public Task DownloadAsync()
            {
                if (ThrowException)
                    throw new InvalidOperationException("Download failed");
                DownloadCalled = true;
                return Task.CompletedTask;
            }
        }

        // Test scenario: CLI file does not exist, so it should download the file.
        [TestMethod]
        public async Task Check_FileDoesNotExist_ShouldDownloadFile()
        {
            // ARRANGE
            var fakeLogger = new FakeLogger();
            var fakeSettings = new FakeCliSettingsProvider();
            // Create a random file path that does not exist.
            var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".exe");
            fakeSettings.CliFileFullPath = tempFilePath;
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);

            var fakeExecuter = new FakeCliExecuter { VersionToReturn = fakeSettings.RequiredDevToolVersion };
            var fakeDownloader = new FakeCliDownloader();

            var fileChecker = new CliFileChecker(fakeLogger, fakeSettings, fakeExecuter, fakeDownloader);

            // ACT
            await fileChecker.Check();

            // ASSERT
            Assert.IsTrue(fakeDownloader.DownloadCalled, "DownloadAsync should have been called when the CLI file does not exist.");
            CollectionAssert.Contains(fakeLogger.InfoMessages, "Cli file doesn't exist. Downloading file...",
                "Logger should record that the CLI file does not exist.");
            CollectionAssert.Contains(fakeLogger.InfoMessages, "Downloaded cli file.",
                "Logger should record that the CLI file was downloaded.");
        }

        // Test scenario: CLI file exists and its version matches the required version.
        [TestMethod]
        public async Task Check_FileExistsAndVersionMatches_ShouldNotDownloadOrDeleteFile()
        {
            // ARRANGE
            var fakeLogger = new FakeLogger();
            var fakeSettings = new FakeCliSettingsProvider();
            // Create a temporary file to simulate an existing CLI file.
            var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".exe");
            File.WriteAllText(tempFilePath, "dummy content");
            fakeSettings.CliFileFullPath = tempFilePath;

            var fakeExecuter = new FakeCliExecuter { VersionToReturn = fakeSettings.RequiredDevToolVersion };
            var fakeDownloader = new FakeCliDownloader();

            var fileChecker = new CliFileChecker(fakeLogger, fakeSettings, fakeExecuter, fakeDownloader);

            // ACT
            await fileChecker.Check();

            // ASSERT
            Assert.IsFalse(fakeDownloader.DownloadCalled, "DownloadAsync should not be called when the CLI file exists and its version matches.");
            CollectionAssert.Contains(fakeLogger.InfoMessages, $"File with required version:{fakeSettings.RequiredDevToolVersion} already exists.",
                "Logger should record that the file with the required version already exists.");

            // Cleanup
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);
        }

        // Test scenario: CLI file exists but its version is outdated.
        [TestMethod]
        public async Task Check_FileExistsAndVersionMismatch_ShouldDeleteAndDownloadFile()
        {
            // ARRANGE
            var fakeLogger = new FakeLogger();
            var fakeSettings = new FakeCliSettingsProvider();
            // Create a temporary file to simulate an existing CLI file.
            var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".exe");
            File.WriteAllText(tempFilePath, "dummy content");
            fakeSettings.CliFileFullPath = tempFilePath;

            // Return a different version to simulate mismatch.
            var fakeExecuter = new FakeCliExecuter { VersionToReturn = "different_version" };
            var fakeDownloader = new FakeCliDownloader();

            var fileChecker = new CliFileChecker(fakeLogger, fakeSettings, fakeExecuter, fakeDownloader);

            // ACT
            await fileChecker.Check();

            // ASSERT: The file should have been deleted and a new download initiated.
            Assert.IsFalse(File.Exists(tempFilePath), "The CLI file should have been deleted due to version mismatch.");
            Assert.IsTrue(fakeDownloader.DownloadCalled, "DownloadAsync should have been called after deleting the outdated CLI file.");
            CollectionAssert.Contains(fakeLogger.InfoMessages, "Downloaded a new version of cli file.",
                "Logger should record that a new version of the CLI file was downloaded.");
        }

        // Test scenario: An exception occurs during the download process.
        [TestMethod]
        public async Task Check_WhenExceptionThrown_ShouldLogError()
        {
            // ARRANGE
            var fakeLogger = new FakeLogger();
            var fakeSettings = new FakeCliSettingsProvider();
            // Use a file path that does not exist.
            var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".exe");
            fakeSettings.CliFileFullPath = tempFilePath;
            if (File.Exists(tempFilePath))
                File.Delete(tempFilePath);

            var fakeExecuter = new FakeCliExecuter { VersionToReturn = fakeSettings.RequiredDevToolVersion };
            // Configure downloader to throw an exception.
            var fakeDownloader = new FakeCliDownloader { ThrowException = true };

            var fileChecker = new CliFileChecker(fakeLogger, fakeSettings, fakeExecuter, fakeDownloader);

            // ACT
            await fileChecker.Check();

            // ASSERT: An error should have been logged.
            Assert.IsTrue(fakeLogger.ErrorMessages.Count > 0, "An error should be logged when an exception occurs during download.");
            var errorLog = fakeLogger.ErrorMessages[0];
            StringAssert.Contains(errorLog.Message, "Error downloading artifact file",
                "Logger should record an error message regarding the download failure.");
            Assert.IsInstanceOfType(errorLog.Ex, typeof(InvalidOperationException));
        }
    }
}
