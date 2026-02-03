// Copyright (c) CodeScene. All rights reserved.

using System.Reflection;
using Codescene.VSExtension.Core.Application.Cli;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliSettingsProviderTests
    {
        [TestMethod]
        public void RequiredDevToolVersion_ShouldReturnExpectedValue()
        {
            // ARRANGE: Create an instance of CliSettingsProvider
            var provider = new CliSettingsProvider();
            var expectedVersion = "b98bdcaf4ac46597a73113d6fca6635d3f3393a5";

            // ACT: Get the value of RequiredDevToolVersion
            var actualVersion = provider.RequiredDevToolVersion;

            // ASSERT: Verify that the returned value is as expected
            Assert.AreEqual(expectedVersion, actualVersion, "RequiredDevToolVersion should return the expected SHA string.");
        }

        [TestMethod]
        public void CliArtifactName_ShouldReturnExpectedValue()
        {
            // ARRANGE: Create an instance and define the expected artifact name
            var provider = new CliSettingsProvider();
            var expectedArtifactName = "cs-ide-windows-amd64-b98bdcaf4ac46597a73113d6fca6635d3f3393a5.zip";

            // ACT: Get the value of CliArtifactName
            var actualArtifactName = provider.CliArtifactName;

            // ASSERT: Ensure the artifact name matches the expected value
            Assert.AreEqual(expectedArtifactName, actualArtifactName, "CliArtifactName should return the expected artifact name.");
        }

        [TestMethod]
        public void CliArtifactUrl_ShouldReturnExpectedValue()
        {
            // ARRANGE: Create an instance and set the expected URL
            var provider = new CliSettingsProvider();
            var expectedArtifactUrl = "https://downloads.codescene.io/enterprise/cli/cs-ide-windows-amd64-b98bdcaf4ac46597a73113d6fca6635d3f3393a5.zip";

            // ACT: Get the value of CliArtifactUrl
            var actualArtifactUrl = provider.CliArtifactUrl;

            // ASSERT: Verify that the URL is constructed correctly
            Assert.AreEqual(expectedArtifactUrl, actualArtifactUrl, "CliArtifactUrl should be the base URL followed by the artifact name.");
        }

        [TestMethod]
        public void CliFileName_ShouldReturnExpectedValue()
        {
            // ARRANGE: Create an instance and define the expected file name
            var provider = new CliSettingsProvider();
            var expectedFileName = "cs-ide.exe";

            // ACT: Get the value of CliFileName
            var actualFileName = provider.CliFileName;

            // ASSERT: Ensure that the file name is correct
            Assert.AreEqual(expectedFileName, actualFileName, "CliFileName should return the expected executable name.");
        }

        [TestMethod]
        public void ArtifactBaseUrl_ShouldReturnExpectedValue()
        {
            // ARRANGE: Create an instance and set the expected base URL
            var provider = new CliSettingsProvider();
            var expectedBaseUrl = "https://downloads.codescene.io/enterprise/cli/";

            // ACT: Retrieve the ArtifactBaseUrl
            var actualBaseUrl = provider.ArtifactBaseUrl;

            // ASSERT: Verify that the base URL matches the expected value
            Assert.AreEqual(expectedBaseUrl, actualBaseUrl, "ArtifactBaseUrl should return the expected base URL.");
        }

        [TestMethod]
        public void CliFileFullPath_ShouldReturnCorrectPath()
        {
            // ARRANGE: Create an instance of CliSettingsProvider
            var provider = new CliSettingsProvider();

            // Determine the directory of the executing assembly
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            var directoryPath = Path.GetDirectoryName(assemblyLocation);

            // Expected full path is the directory combined with the CLI file name
            var expectedFullPath = Path.Combine(directoryPath, provider.CliFileName);

            // ACT: Retrieve the CliFileFullPath value
            var actualFullPath = provider.CliFileFullPath;

            // ASSERT: Check if the computed full path is correct
            Assert.AreEqual(expectedFullPath, actualFullPath, "CliFileFullPath should return the correct full path to the CLI file.");
        }
    }
}
