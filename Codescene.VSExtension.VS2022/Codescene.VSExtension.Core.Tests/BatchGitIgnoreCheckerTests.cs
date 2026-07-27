// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Git;
using LibGit2Sharp;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class BatchGitIgnoreCheckerTests
    {
        private string _testRepoPath = null!;
        private Mock<ILogger> _mockLogger = null!;
        private TestBatchGitIgnoreChecker _checker = null!;

        [TestInitialize]
        public void Setup()
        {
            _testRepoPath = Path.Combine(Path.GetTempPath(), $"test-repo-{Guid.NewGuid()}");
            Directory.CreateDirectory(_testRepoPath);

            Repository.Init(_testRepoPath);

            using (var repo = new Repository(_testRepoPath))
            {
                repo.Config.Set("user.email", "test@example.com");
                repo.Config.Set("user.name", "Test User");
            }

            _mockLogger = new Mock<ILogger>();
            _checker = new TestBatchGitIgnoreChecker(_mockLogger.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _checker = null!;
            if (Directory.Exists(_testRepoPath))
            {
                try
                {
                    ForceDeleteDirectory(_testRepoPath);
                }
                catch
                {
                }
            }
        }

        [TestMethod]
        public void FilterIgnored_EmptyInput_ReturnsEmptySet()
        {
            var result = _checker.FilterIgnored(Array.Empty<string>());

            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void FilterIgnored_SingleNonIgnoredFile_ReturnsFile()
        {
            var filePath = Path.Combine(_testRepoPath, "test.cs");
            File.WriteAllText(filePath, "content");

            var result = _checker.FilterIgnored(new[] { filePath });

            Assert.HasCount(1, result);
            CollectionAssert.Contains(result.ToList(), filePath);
        }

        [TestMethod]
        public void FilterIgnored_SingleIgnoredFile_FiltersOut()
        {
            File.WriteAllText(Path.Combine(_testRepoPath, ".gitignore"), "*.log\n");
            var ignoredPath = Path.Combine(_testRepoPath, "test.log");
            File.WriteAllText(ignoredPath, "content");

            var result = _checker.FilterIgnored(new[] { ignoredPath });

            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void FilterIgnored_MixedFiles_FiltersCorrectly()
        {
            File.WriteAllText(Path.Combine(_testRepoPath, ".gitignore"), "*.log\n");
            var trackedPath = Path.Combine(_testRepoPath, "test.cs");
            var ignoredPath = Path.Combine(_testRepoPath, "test.log");
            File.WriteAllText(trackedPath, "content");
            File.WriteAllText(ignoredPath, "content");

            var result = _checker.FilterIgnored(new[] { trackedPath, ignoredPath });

            Assert.HasCount(1, result);
            CollectionAssert.Contains(result.ToList(), trackedPath);
            CollectionAssert.DoesNotContain(result.ToList(), ignoredPath);
        }

        [TestMethod]
        public void FilterIgnored_MultipleFilesFromSameRepo_UseSingleRepository()
        {
            var paths = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                var filePath = Path.Combine(_testRepoPath, $"file{i}.cs");
                File.WriteAllText(filePath, "content");
                paths.Add(filePath);
            }

            var result = _checker.FilterIgnored(paths);

            Assert.HasCount(10, result);
            Assert.AreEqual(1, _checker.RepositoryOpenCount, "Should reuse a single Repository instance");
        }

        [TestMethod]
        public void FilterIgnored_FileInGitDirectory_FiltersOut()
        {
            var gitConfigPath = Path.Combine(_testRepoPath, ".git", "config");

            var result = _checker.FilterIgnored(new[] { gitConfigPath });

            Assert.IsEmpty(result);
        }

        [TestMethod]
        public void FilterIgnored_NonRepoFile_PassesThrough()
        {
            var tempPath = Path.Combine(Path.GetTempPath(), $"non-repo-{Guid.NewGuid()}.cs");
            try
            {
                File.WriteAllText(tempPath, "content");

                var result = _checker.FilterIgnored(new[] { tempPath });

                Assert.HasCount(1, result);
                CollectionAssert.Contains(result.ToList(), tempPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        [TestMethod]
        public void FilterIgnored_CaseInsensitiveResult_ContainsOriginalPath()
        {
            var filePath = Path.Combine(_testRepoPath, "Test.cs");
            File.WriteAllText(filePath, "content");

            var result = _checker.FilterIgnored(new[] { filePath });

            Assert.HasCount(1, result, "Should contain exactly one file");
            Assert.AreEqual(1, result.Count(p => p.Equals(filePath, StringComparison.OrdinalIgnoreCase)), "Should contain the path (case-insensitive match)");
            Assert.AreEqual(1, result.Count(p => p.Equals(filePath.ToLower(), StringComparison.OrdinalIgnoreCase)), "Lowercase lookup should match");
            Assert.AreEqual(1, result.Count(p => p.Equals(filePath.ToUpper(), StringComparison.OrdinalIgnoreCase)), "Uppercase lookup should match");
        }

        private static void ForceDeleteDirectory(string path)
        {
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(path, true);
        }

        private static string? TryDiscoverRepositoryPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            try
            {
                return Repository.Discover(filePath);
            }
            catch (LibGit2SharpException)
            {
                return null;
            }
        }

        private static bool IsInGitDirectory(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            var separator = Path.DirectorySeparatorChar;
            var gitDirPattern = separator + ".git" + separator;

            return filePath.IndexOf(gitDirPattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetRelativePath(string basePath, string fullPath)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(fullPath))
            {
                return fullPath;
            }

            try
            {
                var baseUri = new Uri(AppendDirectorySeparatorChar(basePath));
                var fullUri = new Uri(fullPath);
                var relativeUri = baseUri.MakeRelativeUri(fullUri);
                return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch
            {
                return fullPath;
            }
        }

        private static string AppendDirectorySeparatorChar(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                return path + Path.DirectorySeparatorChar;
            }

            return path;
        }

        private class TestBatchGitIgnoreChecker : IBatchGitIgnoreChecker
        {
            private readonly ILogger _logger;

            public TestBatchGitIgnoreChecker(ILogger logger)
            {
                _logger = logger;
            }

            public int RepositoryOpenCount { get; private set; }

            public HashSet<string> FilterIgnored(IEnumerable<string> absolutePaths)
            {
                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var pathList = absolutePaths.ToList();

                if (pathList.Count == 0)
                {
                    return result;
                }

                var pathsByRepo = GroupByRepository(pathList);

                foreach (var group in pathsByRepo)
                {
                    var repoPath = group.Key;
                    if (string.IsNullOrEmpty(repoPath))
                    {
                        result.UnionWith(group.Value);
                        continue;
                    }

                    try
                    {
                        RepositoryOpenCount++;
                        using (var repo = new Repository(repoPath))
                        {
                            var repoRoot = repo.Info.WorkingDirectory;
                            if (string.IsNullOrEmpty(repoRoot))
                            {
                                result.UnionWith(group.Value);
                                continue;
                            }

                            foreach (var absolutePath in group.Value)
                            {
                                if (IsInGitDirectory(absolutePath))
                                {
                                    continue;
                                }

                                var relativePath = GetRelativePath(repoRoot, absolutePath)
                                    .Replace("\\", "/").Trim();

                                if (string.IsNullOrEmpty(relativePath))
                                {
                                    relativePath = ".";
                                }

                                if (!repo.Ignore.IsPathIgnored(relativePath))
                                {
                                    result.Add(absolutePath);
                                }
                            }
                        }
                    }
                    catch (LibGit2SharpException ex)
                    {
                        _logger.Warn($"BatchGitIgnoreChecker: LibGit2Sharp error for repo {repoPath}: {ex.Message}");
                        result.UnionWith(group.Value);
                    }
                }

                return result;
            }

            private Dictionary<string, List<string>> GroupByRepository(List<string> paths)
            {
                var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (var path in paths)
                {
                    var repoPath = TryDiscoverRepositoryPath(path);
                    var key = repoPath ?? string.Empty;

                    if (!result.ContainsKey(key))
                    {
                        result[key] = new List<string>();
                    }

                    result[key].Add(path);
                }

                return result;
            }
        }
    }
}
