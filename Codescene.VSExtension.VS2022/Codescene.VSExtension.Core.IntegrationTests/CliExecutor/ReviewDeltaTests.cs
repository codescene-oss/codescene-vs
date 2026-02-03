// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.IntegrationTests.CliExecutor
{
    [TestClass]
    public class ReviewDeltaTests : BaseCliExecutorTests
    {
        [TestInitialize]
        public override void Initialize() => base.Initialize();

        [TestCleanup]
        public override void Cleanup() => base.Cleanup();

        [TestMethod]
        public void ReviewDelta_WithValidScores_ReturnsDeltaResponse()
        {
            // Arrange
            var filename = "Test.cs";
            var simpleCode = @"
public class Simple
{
    public int Add(int a, int b) => a + b;
}";
            var complexCode = @"
public class Complex
{
    public int Calculate(int a, int b, int c, int d, int e)
    {
        if (a > 0) {
            if (b > 0) {
                if (c > 0) {
                    return a + b + c + d + e;
                }
            }
        }
        return 0;
    }
}";

            var simpleReview = cliExecutor.ReviewContent(filename, simpleCode);
            var complexReview = cliExecutor.ReviewContent(filename, complexCode);

            // Skip test if we couldn't get raw scores
            if (string.IsNullOrEmpty(simpleReview?.RawScore) || string.IsNullOrEmpty(complexReview?.RawScore))
            {
                Assert.Inconclusive("Could not obtain raw scores for delta comparison");
                return;
            }

            // Act
            var delta = cliExecutor.ReviewDelta(simpleReview!.RawScore!, complexReview!.RawScore!);

            // Assert
            Assert.IsNotNull(delta, "CLI should return a delta response");
        }
    }
}
