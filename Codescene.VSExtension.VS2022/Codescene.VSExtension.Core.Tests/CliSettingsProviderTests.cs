// Copyright (c) CodeScene. All rights reserved.

using System.Reflection;
using System.Text.RegularExpressions;
using Codescene.VSExtension.Core.Application.Cli;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliSettingsProviderTests
    {
        private readonly string expectedVersion = "8be3c200a676419baf2892b5fb6cb0b77eb26d1a";
        private readonly string expectedCliBinarySha256 = "d8ae7566bfeacfc78fe8dc1e577f1f92d10b1db24d08872353d8d96a19fbb092";

        [TestMethod]
        public void RequiredDevToolVersion_ShouldReturnExpectedValue()
        {
            // ARRANGE: Create an instance of CliSettingsProvider
            var provider = new CliSettingsProvider();

            // ACT: Get the value of RequiredDevToolVersion
            var actualVersion = provider.RequiredDevToolVersion;

            // ASSERT: Verify that the returned value is as expected
            Assert.AreEqual(expectedVersion, actualVersion, "RequiredDevToolVersion should return the expected SHA string.");
        }

        [TestMethod]
        public void RequiredCliBinarySha256_ShouldReturnExpectedValue()
        {
            var provider = new CliSettingsProvider();

            Assert.AreEqual(expectedCliBinarySha256, provider.RequiredCliBinarySha256);
        }

        [TestMethod]
        public void CliArtifactName_ShouldReturnExpectedValue()
        {
            // ARRANGE: Create an instance and define the expected artifact name
            var provider = new CliSettingsProvider();
            var expectedArtifactName = $"cs-ide-windows-amd64-{expectedVersion}.zip";

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
            var expectedArtifactUrl = $"https://downloads.codescene.io/enterprise/cli/cs-ide-windows-amd64-{expectedVersion}.zip";

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

        [TestMethod]
        public void RequiredAgentVersion_IsSemver()
        {
            var provider = new CliSettingsProvider();
            var version = provider.RequiredAgentVersion;

            var semverPattern = @"^\d+\.\d+\.\d+$";
            Assert.IsTrue(Regex.IsMatch(version, semverPattern), $"RequiredAgentVersion '{version}' should match semantic versioning format (MAJOR.MINOR.PATCH).");
        }

        [TestMethod]
        public void RequiredAgentBinarySha256_Is64LowercaseHex()
        {
            var provider = new CliSettingsProvider();
            var sha = provider.RequiredAgentBinarySha256;

            Assert.AreEqual(64, sha.Length, "RequiredAgentBinarySha256 should be exactly 64 characters long.");
            var hexPattern = @"^[0-9a-f]{64}$";
            Assert.IsTrue(Regex.IsMatch(sha, hexPattern), $"RequiredAgentBinarySha256 '{sha}' should consist of 64 lowercase hexadecimal characters.");
        }

        [TestMethod]
        public void AgentFileFullPath_IsNextToCliBinary()
        {
            var provider = new CliSettingsProvider();

            var cliDirectory = Path.GetDirectoryName(provider.CliFileFullPath);
            var agentDirectory = Path.GetDirectoryName(provider.AgentFileFullPath);

            Assert.AreEqual(cliDirectory, agentDirectory, "AgentFileFullPath should be in the same directory as CliFileFullPath.");

            var expectedAgentPath = Path.Combine(cliDirectory, provider.AgentFileName);
            Assert.AreEqual(expectedAgentPath, provider.AgentFileFullPath, "AgentFileFullPath should combine the CLI directory with the agent file name.");
        }
    }
}
