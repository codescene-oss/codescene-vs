using LibGit2Sharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Tests
{
    [TestClass]
    public class GitChangeDetectorChangedFilesTests : GitChangeDetectorTestBase
    {
        [TestMethod]
        public async Task GetChangedFiles_FindsMergeBaseWithMainBranch()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-test");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            CommitFile("feature.cs", "public class Feature {}", "Add feature");

            var changedFiles = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsTrue(changedFiles.Any(f => f.EndsWith("feature.cs")),
                "Should detect feature.cs as changed vs main branch");
        }

        [TestMethod]
        public async Task GetChangedFiles_IteratesToSecondCandidate()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var developBranch = repo.CreateBranch("develop");
                LibGit2Sharp.Commands.Checkout(repo, developBranch);
            }

            try
            {
                ExecGit($"branch -D master");
            }
            catch
            {
            }

            try
            {
                ExecGit($"branch -D main");
            }
            catch
            {
            }

            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-test");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            CommitFile("feature.cs", "public class Feature {}", "Add feature");

            var changedFiles = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsTrue(changedFiles.Any(f => f.EndsWith("feature.cs")),
                "Should find merge base with 'develop' branch when main/master not available");
        }

        [TestMethod]
        public async Task GetChangedFiles_HandlesDetachedHead()
        {
            string commitSha;
            using (var repo = new Repository(_testRepoPath))
            {
                commitSha = repo.Head.Tip.Sha;
            }

            ExecGit($"checkout {commitSha}");

            var filePath = Path.Combine(_testRepoPath, "detached.cs");
            File.WriteAllText(filePath, "public class Detached {}");

            var changedFiles = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(changedFiles,
                "Should return results even in detached HEAD state");
            Assert.IsTrue(changedFiles.Any(f => f.EndsWith("detached.cs")),
                "Should detect new file in detached HEAD state");
        }

        [DataRow(InvalidPathScenario.Null, DisplayName = "Null git root path returns empty list")]
        [DataRow(InvalidPathScenario.Empty, DisplayName = "Empty git root path returns empty list")]
        [DataRow(InvalidPathScenario.NonExistent, DisplayName = "Non-existent directory returns empty list")]
        [DataRow(InvalidPathScenario.NoGitRepository, DisplayName = "No git repository returns empty list")]
        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_InvalidPaths_ReturnsEmptyList(InvalidPathScenario scenario)
        {
            var path = GetPathForScenario(scenario);

            try
            {
                var result = await _detector.GetChangedFilesVsBaselineAsync(
                    path, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

                Assert.IsNotNull(result);
                Assert.AreEqual(0, result.Count);
            }
            finally
            {
                CleanupPathForScenario(scenario, path);
            }
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_CorruptedRepository_ReturnsEmptyListAndLogsWarning()
        {
            var gitObjectsPath = Path.Combine(_testRepoPath, ".git", "objects");
            if (Directory.Exists(gitObjectsPath))
            {
                try
                {
                    Directory.Delete(gitObjectsPath, true);
                }
                catch
                {
                }
            }

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_NullSavedFilesTracker_HandledGracefully()
        {
            CommitFile("test.cs", "public class Test {}", "Add test file");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, null, _fakeOpenFilesObserver);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_NullOpenFilesObserver_HandledGracefully()
        {
            CommitFile("test.cs", "public class Test {}", "Add test file");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, null);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_ExcludesSavedFiles()
        {
            var testFilePath = CommitFile("test.cs", "public class Test {}", "Add test file");

            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-test");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            CommitFile("feature.cs", "public class Feature {}", "Add feature");

            File.WriteAllText(testFilePath, "public class Test { /* modified */ }");
            _fakeSavedFilesTracker.AddSavedFile(testFilePath);

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            var normalizedResult = result.Select(f => f.Replace('/', '\\')).ToList();
            Assert.IsFalse(normalizedResult.Any(f => f.EndsWith("test.cs")),
                "Should exclude saved file from results");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_ExcludesOpenFiles()
        {
            var testFilePath = CommitFile("test.cs", "public class Test {}", "Add test file");

            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-test");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            CommitFile("feature.cs", "public class Feature {}", "Add feature");

            File.WriteAllText(testFilePath, "public class Test { /* modified */ }");
            _fakeOpenFilesObserver.AddOpenFile(testFilePath);

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            var normalizedResult = result.Select(f => f.Replace('/', '\\')).ToList();
            Assert.IsFalse(normalizedResult.Any(f => f.EndsWith("test.cs")),
                "Should exclude open file from results");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_NoMainBranchCandidates_UsesWorkingDirectoryOnly()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var currentBranch = repo.Head.FriendlyName;
                var featureBranch = repo.CreateBranch("feature-xyz");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            try
            {
                ExecGit("branch -D master");
            }
            catch
            {
            }

            try
            {
                ExecGit("branch -D main");
            }
            catch
            {
            }

            var testFilePath = Path.Combine(_testRepoPath, "newfile.cs");
            File.WriteAllText(testFilePath, "public class NewFile {}");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Any(f => f.EndsWith("newfile.cs")),
                "Should detect working directory changes when no main branch candidates exist");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_UnsupportedFiles_NotIncluded()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-test");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            CommitFile("test.txt", "text file content", "Add text file");
            CommitFile("test.cs", "public class Test {}", "Add cs file");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsTrue(result.Any(f => f.EndsWith("test.cs")),
                "Should include supported .cs file");
            Assert.IsFalse(result.Any(f => f.EndsWith("test.txt")),
                "Should exclude unsupported .txt file");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_MultipleChangedFiles_ReturnsAll()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-test");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            CommitFile("file1.cs", "public class File1 {}", "Add file1");
            CommitFile("file2.js", "function test() {}", "Add file2");
            CommitFile("file3.py", "def test():\n    pass", "Add file3");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsTrue(result.Any(f => f.EndsWith("file1.cs")));
            Assert.IsTrue(result.Any(f => f.EndsWith("file2.js")));
            Assert.IsTrue(result.Any(f => f.EndsWith("file3.py")));
        }

        public enum InvalidPathScenario
        {
            Null,
            Empty,
            NonExistent,
            NoGitRepository
        }

        private string GetPathForScenario(InvalidPathScenario scenario)
        {
            switch (scenario)
            {
                case InvalidPathScenario.Null:
                    return null;
                case InvalidPathScenario.Empty:
                    return "";
                case InvalidPathScenario.NonExistent:
                    return Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}");
                case InvalidPathScenario.NoGitRepository:
                    var tempDir = Path.Combine(Path.GetTempPath(), $"no-git-{Guid.NewGuid()}");
                    Directory.CreateDirectory(tempDir);
                    return tempDir;
                default:
                    throw new ArgumentException($"Unknown scenario: {scenario}");
            }
        }

        private void CleanupPathForScenario(InvalidPathScenario scenario, string path)
        {
            if (scenario != InvalidPathScenario.NoGitRepository)
            {
                return;
            }

            if (path == null || !Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch
            {
            }
        }
    }
}
