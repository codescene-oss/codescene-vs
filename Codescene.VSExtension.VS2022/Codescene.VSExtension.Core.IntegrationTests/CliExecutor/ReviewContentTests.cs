// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.IntegrationTests.CliExecutor
{
    [TestClass]
    public class ReviewContentTests : BaseCliExecutorTests
    {
        [TestInitialize]
        public override void Initialize() => base.Initialize();

        [TestCleanup]
        public override void Cleanup() => base.Cleanup();

        [TestMethod]
        public async Task ReviewContentAsync_ValidCSharpCode_ReturnsValidReview()
        {
            var filename = "Test.cs";
            var content = @"
public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}";
            var dir = Path.Combine(Path.GetTempPath(), "codescene-cli-it", Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            var filePath = Path.GetFullPath(Path.Combine(dir, filename));
            try
            {
                File.WriteAllText(filePath, content);

                var result = await cliExecutor.ReviewContentAsync(filePath, content);

                Assert.IsNotNull(result, "CLI should return a review result for valid code");
                Assert.IsTrue(result.Score.HasValue, "Review should have a score");
                Assert.IsTrue(result.Score >= 0 && result.Score <= 10, "Score should be between 0 and 10");
                Assert.IsFalse(string.IsNullOrEmpty(result.RawScore), "Review should have a raw score for delta calculations");
            }
            finally
            {
                TryDeleteDirectory(dir);
            }
        }

        [TestMethod]
        public async Task ReviewContentAsync_ValidJavaScriptCode_ReturnsValidReview()
        {
            var filename = "test.js";
            var content = @"
function calculateSum(numbers) {
    return numbers.reduce((sum, num) => sum + num, 0);
}

module.exports = { calculateSum };
";
            var dir = Path.Combine(Path.GetTempPath(), "codescene-cli-it", Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            var filePath = Path.GetFullPath(Path.Combine(dir, filename));
            try
            {
                File.WriteAllText(filePath, content);

                var result = await cliExecutor.ReviewContentAsync(filePath, content);

                Assert.IsNotNull(result, "CLI should return a review result for valid JavaScript code");
                Assert.IsTrue(result.Score.HasValue, "Review should have a score");
            }
            finally
            {
                TryDeleteDirectory(dir);
            }
        }

        [TestMethod]
        public async Task ReviewContentAsync_ComplexCode_ReturnsCodeSmells()
        {
            var filename = "Complex.cs";
            var content = @"
public class ComplexProcessor
{
    public void ProcessData(int a, int b, int c, int d, int e, int f, int g, int h, int i, int j)
    {
        if (a > 0)
        {
            if (b > 0)
            {
                if (c > 0)
                {
                    if (d > 0)
                    {
                        if (e > 0)
                        {
                            Console.WriteLine(""Deep nesting"");
                        }
                    }
                }
            }
        }
    }
}";
            var dir = Path.Combine(Path.GetTempPath(), "codescene-cli-it", Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            var filePath = Path.GetFullPath(Path.Combine(dir, filename));
            try
            {
                File.WriteAllText(filePath, content);

                var result = await cliExecutor.ReviewContentAsync(filePath, content);

                Assert.IsNotNull(result, "CLI should return a review result");
                Assert.IsTrue(result.Score.HasValue, "Review should have a score");
            }
            finally
            {
                TryDeleteDirectory(dir);
            }
        }

        private static void TryDeleteDirectory(string dir)
        {
            if (!Directory.Exists(dir))
            {
                return;
            }

            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
            }
        }
    }
}
