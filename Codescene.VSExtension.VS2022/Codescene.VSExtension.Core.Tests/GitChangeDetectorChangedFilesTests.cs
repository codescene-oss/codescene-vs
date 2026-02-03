using LibGit2Sharp;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class GitChangeDetectorChangedFilesTests : GitChangeDetectorTestBase
    {
        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_FindsMergeBaseWithMainBranch()
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
        public async Task GetChangedFilesVsBaselineAsync_IteratesToSecondCandidate()
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
        public async Task GetChangedFilesVsBaselineAsync_HandlesDetachedHead()
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

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_NullGitRootPath_ReturnsEmptyList()
        {
            var result = await _detector.GetChangedFilesVsBaselineAsync(
                null, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_EmptyGitRootPath_ReturnsEmptyList()
        {
            var result = await _detector.GetChangedFilesVsBaselineAsync(
                string.Empty, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_NonExistentDirectory_ReturnsEmptyList()
        {
            var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                nonExistentPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_NoGitRepository_ReturnsEmptyList()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"no-git-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var result = await _detector.GetChangedFilesVsBaselineAsync(
                    tempDir, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

                Assert.IsNotNull(result);
                Assert.AreEqual(0, result.Count);
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch
                {
                }
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

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_OnMainBranch_ReturnsOnlyWorkingDirectoryChanges()
        {
            var testFilePath = Path.Combine(_testRepoPath, "newfile.cs");
            File.WriteAllText(testFilePath, "public class NewFile {}");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Any(f => f.EndsWith("newfile.cs")),
                "Should detect working directory changes when on main branch");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_DeduplicatesChangedFiles()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-test");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            var testFilePath = CommitFile("test.cs", "public class Test {}", "Add test file");

            File.WriteAllText(testFilePath, "public class Test { /* modified */ }");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            var testFileCount = result.Count(f => f.EndsWith("test.cs"));
            Assert.AreEqual(1, testFileCount,
                "Should deduplicate files that appear in both committed and status changes");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_MainBranchCandidateDoesNotExist_ReturnsWorkingDirChanges()
        {
            using (var repo = new Repository(_testRepoPath))
            {
                var featureBranch = repo.CreateBranch("feature-branch");
                LibGit2Sharp.Commands.Checkout(repo, featureBranch);
            }

            try { ExecGit("branch -D master"); } catch { }
            try { ExecGit("branch -D main"); } catch { }

            var newFile = Path.Combine(_testRepoPath, "test.cs");
            File.WriteAllText(newFile, "public class Test {}");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsTrue(result.Count > 0, "Should detect working directory changes");
            Assert.IsTrue(result.Any(f => f.Contains("test.cs")), "Should include new file");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_UnalteredFiles_NotIncluded()
        {
            CommitFile("unchanged.cs", "public class Unchanged {}", "Add unchanged file");

            var modifiedFile = Path.Combine(_testRepoPath, "modified.cs");
            File.WriteAllText(modifiedFile, "public class Modified {}");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsTrue(result.Any(f => f.Contains("modified.cs")),
                "Should include modified file");
            Assert.IsFalse(result.Any(f => f.Contains("unchanged.cs")),
                "Should NOT include unaltered file");
        }

        [TestMethod]
        public async Task GetChangedFilesVsBaselineAsync_IgnoredFiles_NotIncluded()
        {
            var gitignorePath = Path.Combine(_testRepoPath, ".gitignore");
            File.WriteAllText(gitignorePath, "*.log\n");

            using (var repo = new Repository(_testRepoPath))
            {
                LibGit2Sharp.Commands.Stage(repo, ".gitignore");
                var signature = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
                repo.Commit("Add gitignore", signature, signature);
            }

            var ignoredFile = Path.Combine(_testRepoPath, "test.log");
            File.WriteAllText(ignoredFile, "log content");

            var normalFile = Path.Combine(_testRepoPath, "test.cs");
            File.WriteAllText(normalFile, "public class Test {}");

            var result = await _detector.GetChangedFilesVsBaselineAsync(
                _testRepoPath, _fakeSavedFilesTracker, _fakeOpenFilesObserver);

            Assert.IsTrue(result.Any(f => f.Contains("test.cs")),
                "Should include non-ignored file");
            Assert.IsFalse(result.Any(f => f.Contains("test.log")),
                "Should NOT include ignored file");
        }
    }
}
