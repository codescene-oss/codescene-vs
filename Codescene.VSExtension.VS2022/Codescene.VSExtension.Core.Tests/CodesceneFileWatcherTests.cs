// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Git;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CodesceneFileWatcherTests
    {
        private string _gitRootPath;
        private string _codesceneDir;
        private FakeLogger _logger;

        [TestInitialize]
        public void Setup()
        {
            _gitRootPath = Path.Combine(Path.GetTempPath(), $"codescene-watcher-{Guid.NewGuid()}");
            Directory.CreateDirectory(_gitRootPath);
            _codesceneDir = Path.Combine(_gitRootPath, CodesceneFileWatcher.CodesceneDir);
            Directory.CreateDirectory(_codesceneDir);
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
        public void FileChanged_Fires_WhenCodeHealthRulesFileCreated()
        {
            AssertFileChangedFiresOnCreate(CodesceneFileWatcher.CodeHealthRulesFileName);
        }

        [TestMethod]
        public void FileChanged_Fires_WhenConfigFileCreated()
        {
            AssertFileChangedFiresOnCreate(CodesceneFileWatcher.ConfigFileName);
        }

        [TestMethod]
        public void FileChanged_Fires_WhenCodeHealthRulesFileChanged()
        {
            AssertFileChangedFiresOnChange(CodesceneFileWatcher.CodeHealthRulesFileName, "{}", "{\"rule_sets\":[]}");
        }

        [TestMethod]
        public void FileChanged_Fires_WhenConfigFileChanged()
        {
            AssertFileChangedFiresOnChange(CodesceneFileWatcher.ConfigFileName, "{}", "{\"baseline_branch\":\"develop\"}");
        }

        [TestMethod]
        public void FileChanged_Fires_WhenCodeHealthRulesFileDeleted()
        {
            AssertFileChangedFiresOnDelete(CodesceneFileWatcher.CodeHealthRulesFileName);
        }

        [TestMethod]
        public void FileChanged_Fires_WhenConfigFileDeleted()
        {
            AssertFileChangedFiresOnDelete(CodesceneFileWatcher.ConfigFileName);
        }

        [TestMethod]
        public void Constructor_WhenCodesceneDirMissing_DoesNotThrow()
        {
            var rootWithoutCodescene = Path.Combine(Path.GetTempPath(), $"codescene-watcher-none-{Guid.NewGuid()}");
            Directory.CreateDirectory(rootWithoutCodescene);
            try
            {
                using (var watcher = new CodesceneFileWatcher(rootWithoutCodescene, CodesceneFileWatcher.ConfigFileName, _logger))
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
            var filePath = Path.Combine(_codesceneDir, CodesceneFileWatcher.ConfigFileName);
            File.WriteAllText(filePath, "{}");
            var watcher = new CodesceneFileWatcher(_gitRootPath, CodesceneFileWatcher.ConfigFileName, _logger);
            watcher.Dispose();
            watcher.Dispose();
        }

        [TestMethod]
        public async Task FileChanged_ThrowingHandler_LogsErrorAndDoesNotCrash()
        {
            var filePath = Path.Combine(_codesceneDir, CodesceneFileWatcher.ConfigFileName);
            using (var watcher = new CodesceneFileWatcher(_gitRootPath, CodesceneFileWatcher.ConfigFileName, _logger))
            {
                watcher.FileChanged += (sender, args) => throw new InvalidOperationException("handler error");

                File.WriteAllText(filePath, "{}");

                var deadline = DateTime.UtcNow.AddMilliseconds(3000);
                while (DateTime.UtcNow < deadline)
                {
                    if (_logger.SnapshotErrorMessages().Any(m => m.Item1.Contains("Error in FileChanged handler")))
                    {
                        break;
                    }

                    await Task.Delay(100);
                }

                Assert.IsTrue(
                    _logger.SnapshotErrorMessages().Any(m => m.Item1.Contains("Error in FileChanged handler")),
                    "Error should be logged when handler throws");
            }
        }

        private void AssertFileChangedFiresOnCreate(string fileName)
        {
            var filePath = Path.Combine(_codesceneDir, fileName);
            var eventFired = new ManualResetEventSlim(false);
            using (var watcher = new CodesceneFileWatcher(_gitRootPath, fileName, _logger))
            {
                watcher.FileChanged += (sender, args) => eventFired.Set();
                File.WriteAllText(filePath, "{}");
                Assert.IsTrue(eventFired.Wait(3000), "FileChanged should fire when file is created");
            }
        }

        private void AssertFileChangedFiresOnChange(string fileName, string initialContent, string updatedContent)
        {
            var filePath = Path.Combine(_codesceneDir, fileName);
            File.WriteAllText(filePath, initialContent);
            var eventFired = new ManualResetEventSlim(false);
            using (var watcher = new CodesceneFileWatcher(_gitRootPath, fileName, _logger))
            {
                watcher.FileChanged += (sender, args) => eventFired.Set();
                File.WriteAllText(filePath, updatedContent);
                Assert.IsTrue(eventFired.Wait(3000), "FileChanged should fire when file is changed");
            }
        }

        private void AssertFileChangedFiresOnDelete(string fileName)
        {
            var filePath = Path.Combine(_codesceneDir, fileName);
            File.WriteAllText(filePath, "{}");
            var eventFired = new ManualResetEventSlim(false);
            using (var watcher = new CodesceneFileWatcher(_gitRootPath, fileName, _logger))
            {
                watcher.FileChanged += (sender, args) => eventFired.Set();
                File.Delete(filePath);
                Assert.IsTrue(eventFired.Wait(3000), "FileChanged should fire when file is deleted");
            }
        }
    }
}
