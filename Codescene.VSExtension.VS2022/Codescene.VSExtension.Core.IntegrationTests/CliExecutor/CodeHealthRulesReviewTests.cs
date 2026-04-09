// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Review;
using LibGit2Sharp;

namespace Codescene.VSExtension.Core.IntegrationTests.CliExecutor
{
    [TestClass]
    public class CodeHealthRulesReviewTests : BaseCliExecutorTests
    {
        private const string BumpyRoadSource = """
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Codescene.VSExtension.CodeSmells.Issues.CSharp
{
    class BumpyRoadExample
    {
        public void ProcessDirectory(string path)
        {
            var files = new List<string>();
            var directory = new DirectoryInfo(path);

            foreach (FileInfo fileInfo in directory.GetFiles())
            {
                if (Regex.IsMatch(fileInfo.Name, @"^data\d+\.csv$"))
                {
                    files.Add(fileInfo.FullName);
                }
            }

            var sb = new StringBuilder();
            foreach (string filePath in files)
            {
                using (var reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        sb.Append(line);
                    }
                }
            }

            using (var writer = new StreamWriter("data.csv"))
            {
                writer.Write(sb.ToString());
            }
        }
    }
}
""";

        private const string RulesBumpyRoadWeight0 = """
{
  "rule_sets": [
    {
      "matching_content_path": "**/*",
      "rules": [
        {
          "name": "Bumpy Road Ahead",
          "weight": 0.0
        }
      ]
    }
  ]
}
""";

        private const string RulesBumpyRoadWeight1 = """
{
  "rule_sets": [
    {
      "matching_content_path": "**/*",
      "rules": [
        {
          "name": "Bumpy Road Ahead",
          "weight": 1.0
        }
      ]
    }
  ]
}
""";

        private const string RulesFunctionMaxArguments7 = """
{
  "rule_sets": [
    {
      "matching_content_path": "**/*",
      "rules": [],
      "thresholds": [
        {
          "name": "function_max_arguments",
          "value": "7"
        }
      ]
    }
  ]
}
""";

        private const string EightParameterMethodSource = """
namespace ArgsThreshold;

public static class EightParams
{
    public static void M(int a, int b, int c, int d, int e, int f, int g, int h)
    {
    }
}
""";

        private const string SixParameterMethodSource = """
namespace ArgsThreshold;

public static class SixParams
{
    public static void M(int a, int b, int c, int d, int e, int f)
    {
    }
}
""";

        private string? _gitRepoRoot;

        [TestInitialize]
        public override void Initialize() => base.Initialize();

        [TestCleanup]
        public override void Cleanup()
        {
            TryDeleteGitRepo();
            base.Cleanup();
        }

        [TestMethod]
        public async Task ReviewContentAsync_BumpyRoadWeightZero_ScoreTenNoFunctionLevelSmells()
        {
            var filePath = PrepareGitRepoWithRules(RulesBumpyRoadWeight0, "src/BumpyRoad.cs", BumpyRoadSource);
            try
            {
                var result = await cliExecutor.ReviewContentAsync(filePath, BumpyRoadSource);

                Assert.IsNotNull(result);
                Helpers.AssertNoCodeHealthRulesError(result);
                Assert.AreEqual(10f, result.Score!.Value, 0.01f);
                Assert.IsTrue(
                    result.FunctionLevelCodeSmells == null || result.FunctionLevelCodeSmells.Count == 0,
                    "Bumpy Road weight 0 should yield no function-level code smells");
            }
            finally
            {
                TryDeleteGitRepo();
            }
        }

        [TestMethod]
        public async Task ReviewContentAsync_BumpyRoadWeightOne_RetainsSmell()
        {
            var filePath = PrepareGitRepoWithRules(RulesBumpyRoadWeight1, "src/BumpyRoad.cs", BumpyRoadSource);
            try
            {
                var result = await cliExecutor.ReviewContentAsync(filePath, BumpyRoadSource);

                Assert.IsNotNull(result);
                Helpers.AssertNoCodeHealthRulesError(result);
                Assert.IsTrue(
                    Helpers.TotalSmellCount(result) > 0 || Helpers.HasSmellCategoryContaining(result, "Bumpy Road"),
                    "Bumpy Road should contribute when weight is 1.0");
            }
            finally
            {
                TryDeleteGitRepo();
            }
        }

        [TestMethod]
        public async Task ReviewContentAsync_NoCodeHealthRules_BumpyRoadNotSuppressed()
        {
            var filePath = PrepareGitRepoWithoutRules("src/BumpyRoad.cs", BumpyRoadSource);
            try
            {
                var result = await cliExecutor.ReviewContentAsync(filePath, BumpyRoadSource);

                Assert.IsNotNull(result);
                Helpers.AssertNoCodeHealthRulesError(result);
                Assert.IsTrue(
                    Helpers.TotalSmellCount(result) > 0 || result.Score < 10,
                    "Default rules should surface complexity / Bumpy Road for this fixture");
            }
            finally
            {
                TryDeleteGitRepo();
            }
        }

        [TestMethod]
        public async Task ReviewContentAsync_FunctionMaxArgumentsThreshold_ExceedingLimit_Reported()
        {
            var filePath = PrepareGitRepoWithRules(RulesFunctionMaxArguments7, "src/Args.cs", EightParameterMethodSource);
            try
            {
                var result = await cliExecutor.ReviewContentAsync(filePath, EightParameterMethodSource);

                Assert.IsNotNull(result);
                Helpers.AssertNoCodeHealthRulesError(result);
                Assert.IsTrue(
                    Helpers.HasTooManyArgumentsSmell(result),
                    "Eight parameters should exceed function_max_arguments threshold of 7");
            }
            finally
            {
                TryDeleteGitRepo();
            }
        }

        [TestMethod]
        public async Task ReviewContentAsync_FunctionMaxArgumentsThreshold_UnderLimit_NotReported()
        {
            var filePath = PrepareGitRepoWithRules(RulesFunctionMaxArguments7, "src/Args.cs", SixParameterMethodSource);
            try
            {
                var result = await cliExecutor.ReviewContentAsync(filePath, SixParameterMethodSource);

                Assert.IsNotNull(result);
                Helpers.AssertNoCodeHealthRulesError(result);
                Assert.IsFalse(
                    Helpers.HasTooManyArgumentsSmell(result),
                    "Six parameters should not exceed function_max_arguments threshold of 7");
            }
            finally
            {
                TryDeleteGitRepo();
            }
        }

        private string PrepareGitRepoWithRules(string rulesJson, string sourceRelativePath, string sourceContent)
        {
            TryDeleteGitRepo();
            _gitRepoRoot = Path.Combine(Path.GetTempPath(), "codescene-ch-rules", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_gitRepoRoot);
            Repository.Init(_gitRepoRoot);
            using (var repo = new Repository(_gitRepoRoot))
            {
                repo.Config.Set("user.email", "test@example.com");
                repo.Config.Set("user.name", "Test User");
            }

            WriteRepoFile(".codescene/code-health-rules.json", rulesJson);
            WriteRepoFile(sourceRelativePath, sourceContent);
            StageAndCommit(new[] { ".codescene/code-health-rules.json", sourceRelativePath });

            return Path.GetFullPath(Path.Combine(_gitRepoRoot, sourceRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private string PrepareGitRepoWithoutRules(string sourceRelativePath, string sourceContent)
        {
            TryDeleteGitRepo();
            _gitRepoRoot = Path.Combine(Path.GetTempPath(), "codescene-ch-rules", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_gitRepoRoot);
            Repository.Init(_gitRepoRoot);
            using (var repo = new Repository(_gitRepoRoot))
            {
                repo.Config.Set("user.email", "test@example.com");
                repo.Config.Set("user.name", "Test User");
            }

            WriteRepoFile(sourceRelativePath, sourceContent);
            StageAndCommit(new[] { sourceRelativePath });

            return Path.GetFullPath(Path.Combine(_gitRepoRoot, sourceRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private void WriteRepoFile(string relativePath, string content)
        {
            var fullPath = Path.Combine(_gitRepoRoot!, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(fullPath, content);
        }

        private void StageAndCommit(string[] relativePaths)
        {
            using var repo = new Repository(_gitRepoRoot!);
            foreach (var p in relativePaths)
            {
                Commands.Stage(repo, p);
            }

            var sig = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
            repo.Commit("test", sig, sig);
        }

        private void TryDeleteGitRepo()
        {
            if (string.IsNullOrEmpty(_gitRepoRoot) || !Directory.Exists(_gitRepoRoot))
            {
                return;
            }

            try
            {
                Directory.Delete(_gitRepoRoot, recursive: true);
            }
            catch
            {
            }

            _gitRepoRoot = null;
        }

        private static class Helpers
        {
            public static int TotalSmellCount(CliReviewModel r)
            {
                var n = r.FileLevelCodeSmells?.Count ?? 0;
                if (r.FunctionLevelCodeSmells != null)
                {
                    foreach (var fn in r.FunctionLevelCodeSmells)
                    {
                        if (fn.CodeSmells != null)
                        {
                            n += fn.CodeSmells.Length;
                        }
                    }
                }

                return n;
            }

            public static bool HasSmellCategoryContaining(CliReviewModel r, string substring)
            {
                foreach (var s in r.FileLevelCodeSmells ?? new List<CliCodeSmellModel>())
                {
                    if (s.Category != null && s.Category.Contains(substring, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                foreach (var fn in r.FunctionLevelCodeSmells ?? new List<CliReviewFunctionModel>())
                {
                    if (fn.CodeSmells == null)
                    {
                        continue;
                    }

                    foreach (var s in fn.CodeSmells)
                    {
                        if (s.Category != null && s.Category.Contains(substring, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            public static bool HasTooManyArgumentsSmell(CliReviewModel r)
            {
                return HasSmellCategoryContaining(r, "argument");
            }

            public static void AssertNoCodeHealthRulesError(CliReviewModel r)
            {
                if (r.CodeHealthRulesError != null)
                {
                    Assert.Fail(
                        $"code-health-rules-error: {r.CodeHealthRulesError.Description} remedy: {r.CodeHealthRulesError.Remedy}");
                }
            }
        }
    }
}
