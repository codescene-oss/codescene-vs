// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli.Delta;

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
        public async Task ReviewDeltaAsync_WithValidScores_ReturnsDeltaResponse()
        {
            var filename = "Test.cs";
            var simpleCode = @"
public class Simple
{
    public int Add(int a, int b) => a + b;
}";
            var complexCode = @"
public class Complex
{
    public int Calculate(int a, int b, int c, int d, int e, int f, int g, int h)
    {
        if (a > 0) {
            if (b > 0) {
                if (c > 0) {
                    return a + b + c + d + e + f + g + h;
                }
            }
        }
        return 0;
    }
}";

            var dir = Path.Combine(Path.GetTempPath(), "codescene-cli-it", Guid.NewGuid().ToString());
            Directory.CreateDirectory(dir);
            var filePath = Path.GetFullPath(Path.Combine(dir, filename));
            try
            {
                File.WriteAllText(filePath, simpleCode);
                var simpleReview = await cliExecutor.ReviewContentAsync(filePath, simpleCode);

                File.WriteAllText(filePath, complexCode);
                var complexReview = await cliExecutor.ReviewContentAsync(filePath, complexCode);

                if (string.IsNullOrEmpty(simpleReview?.RawScore) || string.IsNullOrEmpty(complexReview?.RawScore))
                {
                    Assert.Inconclusive("Could not obtain raw scores for delta comparison");
                    return;
                }

                var delta = await cliExecutor.ReviewDeltaAsync(new ReviewDeltaRequest { OldScore = simpleReview!.RawScore!, NewScore = complexReview!.RawScore! });

                Assert.IsNotNull(delta, "CLI should return a delta response");
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
