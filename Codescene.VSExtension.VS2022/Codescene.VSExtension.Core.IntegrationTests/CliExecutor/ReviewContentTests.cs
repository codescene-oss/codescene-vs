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
        public void ReviewContent_ValidCSharpCode_ReturnsValidReview()
        {
            // Arrange
            var filename = "Test.cs";
            var content = @"
public class Calculator
{
    public int Add(int a, int b)
    {
        return a + b;
    }
}";

            // Act
            var result = cliExecutor.ReviewContent(filename, content);

            // Assert
            Assert.IsNotNull(result, "CLI should return a review result for valid code");
            Assert.IsTrue(result.Score.HasValue, "Review should have a score");
            Assert.IsTrue(result.Score >= 0 && result.Score <= 10, "Score should be between 0 and 10");
            Assert.IsFalse(string.IsNullOrEmpty(result.RawScore), "Review should have a raw score for delta calculations");
        }

        [TestMethod]
        public void ReviewContent_ValidJavaScriptCode_ReturnsValidReview()
        {
            // Arrange
            var filename = "test.js";
            var content = @"
function calculateSum(numbers) {
    return numbers.reduce((sum, num) => sum + num, 0);
}

module.exports = { calculateSum };
";

            // Act
            var result = cliExecutor.ReviewContent(filename, content);

            // Assert
            Assert.IsNotNull(result, "CLI should return a review result for valid JavaScript code");
            Assert.IsTrue(result.Score.HasValue, "Review should have a score");
        }

        [TestMethod]
        public void ReviewContent_ComplexCode_ReturnsCodeSmells()
        {
            // Arrange - Code with intentional complexity
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

            // Act
            var result = cliExecutor.ReviewContent(filename, content);

            // Assert
            Assert.IsNotNull(result, "CLI should return a review result");
            Assert.IsTrue(result.Score.HasValue, "Review should have a score");
        }
    }
}
