// Copyright (c) CodeScene. All rights reserved.

using System.Reflection;
using Codescene.VSExtension.Core.Application.Cli;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliSettingsProviderTests
    {
        private readonly string expectedVersion = "21ac9263043b0bca2f216edbd61255f54edc77b3";

        [TestMethod]
        public void RequiredDevToolVersion_ShouldReturnExpectedValue()
        {
            var provider = new CliSettingsProvider();
            Assert.AreEqual(expectedVersion, provider.RequiredDevToolVersion);
        }

        [TestMethod]
        public void CliArtifactName_ShouldReturnJreZipName()
        {
            var provider = new CliSettingsProvider();
            Assert.AreEqual($"cs-ide-jre-windows-amd64-{expectedVersion}.zip", provider.CliArtifactName);
        }

        [TestMethod]
        public void DistributionPaths_AreUnderCsWindowsAmd64()
        {
            var provider = new CliSettingsProvider();
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Assert.AreEqual(Path.Combine(assemblyDir, "cs-windows-amd64"), provider.DistributionFullPath);
            Assert.AreEqual(Path.Combine(provider.DistributionFullPath, "jre", "bin", "java.exe"), provider.JavaExeFullPath);
            Assert.AreEqual(Path.Combine(provider.DistributionFullPath, "cs-ide.jar"), provider.JarFullPath);
        }
    }
}
