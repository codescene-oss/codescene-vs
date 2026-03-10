// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using Codescene.VSExtension.Core.Application.Git;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CodeHealthRulesWatcherTests
    {
        private string _gitRootPath;
        private string _rulesFilePath;
        private FakeLogger _logger;

        [TestInitialize]
        public void Setup()
        {
            _gitRootPath = Path.Combine(Path.GetTempPath(), $"rules-watcher-{System.Guid.NewGuid()}");
            Directory.CreateDirectory(_gitRootPath);
            var codesceneDir = Path.Combine(_gitRootPath, ".codescene");
            Directory.CreateDirectory(codesceneDir);
            _rulesFilePath = Path.Combine(codesceneDir, "code-health-rules.json");
            _logger = new FakeLogger();
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
        public void RulesFileChanged_Fires_WhenFileCreated()
        {
            var eventFired = new System.Threading.ManualResetEventSlim(false);
            using (var watcher = new CodeHealthRulesWatcher(_gitRootPath, _logger))
            {
                watcher.RulesFileChanged += (sender, args) => eventFired.Set();
                File.WriteAllText(_rulesFilePath, "{}");
                Assert.IsTrue(eventFired.Wait(3000), "RulesFileChanged should fire when file is created");
            }
        }

        [TestMethod]
        public void RulesFileChanged_Fires_WhenFileChanged()
        {
            File.WriteAllText(_rulesFilePath, "{}");
            var eventFired = new System.Threading.ManualResetEventSlim(false);
            using (var watcher = new CodeHealthRulesWatcher(_gitRootPath, _logger))
            {
                watcher.RulesFileChanged += (sender, args) => eventFired.Set();
                File.WriteAllText(_rulesFilePath, "{\"rule_sets\":[]}");
                Assert.IsTrue(eventFired.Wait(3000), "RulesFileChanged should fire when file is changed");
            }
        }

        [TestMethod]
        public void RulesFileChanged_Fires_WhenFileDeleted()
        {
            File.WriteAllText(_rulesFilePath, "{}");
            var eventFired = new System.Threading.ManualResetEventSlim(false);
            using (var watcher = new CodeHealthRulesWatcher(_gitRootPath, _logger))
            {
                watcher.RulesFileChanged += (sender, args) => eventFired.Set();
                File.Delete(_rulesFilePath);
                Assert.IsTrue(eventFired.Wait(3000), "RulesFileChanged should fire when file is deleted");
            }
        }

        [TestMethod]
        public void Constructor_WhenCodesceneDirMissing_DoesNotThrow()
        {
            var rootWithoutCodescene = Path.Combine(Path.GetTempPath(), $"rules-watcher-none-{System.Guid.NewGuid()}");
            Directory.CreateDirectory(rootWithoutCodescene);
            try
            {
                using (var watcher = new CodeHealthRulesWatcher(rootWithoutCodescene, _logger))
                {
                }
            }
            finally
            {
                if (Directory.Exists(rootWithoutCodescene))
                {
                    try
                    {
                        Directory.Delete(rootWithoutCodescene, true);
                    }
                    catch
                    {
                    }
                }
            }
        }

        [TestMethod]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            File.WriteAllText(_rulesFilePath, "{}");
            var watcher = new CodeHealthRulesWatcher(_gitRootPath, _logger);
            watcher.Dispose();
            watcher.Dispose();
        }
    }
}
