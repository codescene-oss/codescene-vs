// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CodesceneRepoConfigTests
    {
        private string _gitRootPath;
        private string _configPath;

        [TestInitialize]
        public void Setup()
        {
            _gitRootPath = Path.Combine(Path.GetTempPath(), $"codescene-config-{Guid.NewGuid()}");
            Directory.CreateDirectory(_gitRootPath);
            var codesceneDir = Path.Combine(_gitRootPath, CodesceneFileWatcher.CodesceneDir);
            Directory.CreateDirectory(codesceneDir);
            _configPath = Path.Combine(codesceneDir, CodesceneFileWatcher.ConfigFileName);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_gitRootPath))
            {
                try
                {
                    Directory.Delete(_gitRootPath, true);
                }
                catch
                {
                }
            }
        }

        [TestMethod]
        public void GetBaselineBranch_WhenFileMissing_ReturnsNull()
        {
            var result = CodesceneRepoConfig.GetBaselineBranch(_gitRootPath);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetBaselineBranch_WhenValid_ReturnsBranchName()
        {
            File.WriteAllText(_configPath, "{\"baseline_branch\":\"develop\"}");

            var result = CodesceneRepoConfig.GetBaselineBranch(_gitRootPath);

            Assert.AreEqual("develop", result);
        }

        [TestMethod]
        public void GetBaselineBranch_WhenWhitespaceOnly_ReturnsNull()
        {
            File.WriteAllText(_configPath, "{\"baseline_branch\":\"   \"}");

            var result = CodesceneRepoConfig.GetBaselineBranch(_gitRootPath);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetBaselineBranch_WhenInvalidJson_ReturnsNull()
        {
            File.WriteAllText(_configPath, "not json");

            var result = CodesceneRepoConfig.GetBaselineBranch(_gitRootPath);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetBaselineBranch_WhenPropertyMissing_ReturnsNull()
        {
            File.WriteAllText(_configPath, "{\"other\":\"value\"}");

            var result = CodesceneRepoConfig.GetBaselineBranch(_gitRootPath);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetBaselineBranch_WhenGitRootNull_ReturnsNull()
        {
            var result = CodesceneRepoConfig.GetBaselineBranch(null);

            Assert.IsNull(result);
        }
    }
}
