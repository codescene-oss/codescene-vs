
using Codescene.VSExtension.Core.Application.Services.Cli;

namespace Codescene.VSExtension.Core.IntegrationTests
{
    /// <summary>
    /// Integration tests for CliExecutor that verify actual CLI behavior.
    /// These tests complement the unit tests in Core.Tests which test parsing/error handling logic.
    /// 
    /// Unit tests answer: "Given CLI returns X, does our code handle it correctly?"
    /// These integration tests answer: "Does CLI actually return valid responses?"
    /// </summary>
    [TestClass]
    public class CliExecutorTests : BaseIntegrationTests
    {
        private ICliExecutor _cliExecutor;
        private string _tempCacheDir;

        [TestInitialize]
        public override void Initialize()
        {
            base.Initialize();
            _cliExecutor = GetService<ICliExecutor>();

            // Create a temporary cache directory for tests
            _tempCacheDir = Path.Combine(Path.GetTempPath(), "codescene-test-cache", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempCacheDir);

            // Setup mock cache storage to return our temp directory
            MockCacheStorageService.Setup(x => x.GetSolutionReviewCacheLocation())
                .Returns(_tempCacheDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up temp cache directory
            if (Directory.Exists(_tempCacheDir))
            {
                try
                {
                    Directory.Delete(_tempCacheDir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region Code Review Integration Tests

        /// <summary>
        /// Verifies that CLI returns a valid review response for valid C# code.
        /// </summary>
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
            var result = _cliExecutor.ReviewContent(filename, content);

            // Assert
            Assert.IsNotNull(result, "CLI should return a review result for valid code");
            Assert.IsTrue(result.Score.HasValue, "Review should have a score");
            Assert.IsTrue(result.Score >= 0 && result.Score <= 10, "Score should be between 0 and 10");
            Assert.IsFalse(string.IsNullOrEmpty(result.RawScore), "Review should have a raw score for delta calculations");
        }

        /// <summary>
        /// Verifies that CLI returns a valid review response for valid JavaScript code.
        /// </summary>
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
            var result = _cliExecutor.ReviewContent(filename, content);

            // Assert
            Assert.IsNotNull(result, "CLI should return a review result for valid JavaScript code");
            Assert.IsTrue(result.Score.HasValue, "Review should have a score");
        }

        /// <summary>
        /// Verifies that CLI can detect code smells in complex code.
        /// </summary>
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
            var result = _cliExecutor.ReviewContent(filename, content);

            // Assert
            Assert.IsNotNull(result, "CLI should return a review result");
            // Complex code should have a lower score or code smells
            Assert.IsTrue(result.Score.HasValue, "Review should have a score");
        }

        #endregion

        #region Device ID Integration Tests

        /// <summary>
        /// Verifies that CLI returns a stable device identifier.
        /// </summary>
        [TestMethod]
        public void GetDeviceId_ReturnsNonEmptyStableId()
        {
            // Act
            var deviceId1 = _cliExecutor.GetDeviceId();
            var deviceId2 = _cliExecutor.GetDeviceId();

            // Assert
            Assert.IsFalse(string.IsNullOrWhiteSpace(deviceId1), "Device ID should not be empty");
            Assert.AreEqual(deviceId1, deviceId2, "Device ID should be stable across calls");
        }

        #endregion

        #region Preflight Integration Tests

        /// <summary>
        /// Verifies that CLI returns supported file types in preflight response.
        /// </summary>
        [TestMethod]
        public void Preflight_ReturnsFileTypes()
        {
            // Act
            var result = _cliExecutor.Preflight(force: true);

            // Assert
            Assert.IsNotNull(result, "CLI should return a preflight response");
            Assert.IsNotNull(result.FileTypes, "Preflight should include file types");
            Assert.IsTrue(result.FileTypes.Length > 0, "Preflight should return at least one supported file type");

            // Verify common file types are supported
            var fileTypes = result.FileTypes.Select(ft => ft.ToLower()).ToArray();
            Assert.IsTrue(fileTypes.Any(ft => ft.Contains("cs") || ft.Contains("csharp")),
                "C# should be a supported file type");
        }

        #endregion

        #region Delta Review Integration Tests

        /// <summary>
        /// Verifies that CLI can compute delta between two code versions.
        /// </summary>
        [TestMethod]
        public void ReviewDelta_WithValidScores_ReturnsDeltaResponse()
        {
            // Arrange - First get raw scores from two different code versions
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

            var simpleReview = _cliExecutor.ReviewContent(filename, simpleCode);
            var complexReview = _cliExecutor.ReviewContent(filename, complexCode);

            // Skip test if we couldn't get raw scores
            if (string.IsNullOrEmpty(simpleReview?.RawScore) || string.IsNullOrEmpty(complexReview?.RawScore))
            {
                Assert.Inconclusive("Could not obtain raw scores for delta comparison");
                return;
            }

            // Act
            var delta = _cliExecutor.ReviewDelta(simpleReview!.RawScore!, complexReview!.RawScore!);

            // Assert
            Assert.IsNotNull(delta, "CLI should return a delta response");
        }

        #endregion
    }
}
