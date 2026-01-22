using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Moq;
using System.Globalization;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliCommandProviderTests
    {
        private CliCommandProvider _commandProvider;
        private Mock<ILogger> _mockLogger;

        private const string TestFileName = "js";
        private const string TestFileContent = "content";
        private const string TestCachePath = "/home/user/cache";

        private static List<CliCodeSmellModel> CreateTestCodeSmells()
        {
            return new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "test" } };
        }

        private static PreFlightResponseModel CreateTestPreflight()
        {
            return new PreFlightResponseModel { FileTypes = new[] { ".js" } };
        }

        private static DeltaResponseModel CreateTestDeltaResult()
        {
            return new DeltaResponseModel { NewScore = 2, OldScore = 3 };
        }

        [TestInitialize]
        public void Initialize()
        {
            _mockLogger = new Mock<ILogger>();
            _commandProvider = new CliCommandProvider(new CliObjectScoreCreator(_mockLogger.Object));
        }

        [TestMethod]
        public void VersionCommand_ShouldReturnCorrectString()
        {
            var command = _commandProvider.VersionCommand;

            Assert.AreEqual("version --sha", command);
        }

        [TestMethod]
        public void GetReviewFileContentCommand_ShouldIncludeIdeApiAndFilename()
        {
            var command = _commandProvider.GetReviewFileContentCommand("testfile.txt");

            Assert.AreEqual("review --file-name testfile.txt", command);
        }

        [TestMethod]
        public void GetReviewPathCommand_ShouldIncludeIdeApiAndPath()
        {
            var command = _commandProvider.GetReviewPathCommand("some/path");

            Assert.AreEqual("review some/path", command);
        }

        [TestMethod]
        public void GetReviewFileContentPayload_ReturnsCorrectJson()
        {
            var payload = _commandProvider.GetReviewFileContentPayload(TestFileName, TestFileContent, TestCachePath);

            Assert.AreEqual($"{{\"path\":\"{TestFileName}\",\"file-content\":\"{TestFileContent}\",\"cache-path\":\"{TestCachePath}\"}}", payload);
        }

        [TestMethod]
        public void GetRefactorWithCodeSmellsPayload_Without_Preflight_ReturnsCorrectJson()
        {
            var codeSmells = CreateTestCodeSmells();

            var content = _commandProvider.GetRefactorWithCodeSmellsPayload(TestFileName, TestFileContent, TestCachePath, codeSmells, null);

            Assert.AreEqual($"{{\"code-smells\":[{{\"category\":\"{codeSmells.First().Category}\"}}],\"file-name\":\"{TestFileName}\",\"file-content\":\"{TestFileContent}\",\"cache-path\":\"{TestCachePath}\"}}", content);
        }

        [TestMethod]
        public void GetRefactorWithCodeSmellsPayload_With_Preflight_ReturnsCorrectJson()
        {
            var codeSmells = CreateTestCodeSmells();
            var preflight = CreateTestPreflight();

            var content = _commandProvider.GetRefactorWithCodeSmellsPayload(TestFileName, TestFileContent, TestCachePath, codeSmells, preflight);

            Assert.AreEqual($"{{\"code-smells\":[{{\"category\":\"{codeSmells.First().Category}\"}}],\"file-name\":\"{TestFileName}\",\"file-content\":\"{TestFileContent}\",\"preflight\":{{\"file-types\":[\"{preflight.FileTypes.First()}\"]}},\"cache-path\":\"{TestCachePath}\"}}", content);
        }

        [TestMethod]
        public void GetRefactorWithDeltaResultPayload_Without_Preflight_ReturnsCorrectJson()
        {
            var deltaResult = CreateTestDeltaResult();

            var content = _commandProvider.GetRefactorWithDeltaResultPayload(TestFileName, TestFileContent, TestCachePath, deltaResult, null);

            var expectedNewScore = deltaResult.NewScore.ToString("0.0", CultureInfo.InvariantCulture);
            var expectedOldScore = deltaResult.OldScore.ToString("0.0", CultureInfo.InvariantCulture);
            Assert.AreEqual($"{{\"delta-result\":{{\"new-score\":{expectedNewScore},\"old-score\":{expectedOldScore}}},\"file-name\":\"{TestFileName}\",\"file-content\":\"{TestFileContent}\",\"cache-path\":\"{TestCachePath}\"}}", content);
        }

        [TestMethod]
        public void GetRefactorWithDeltaResultPayload_With_Preflight_ReturnsCorrectJson()
        {
            var deltaResult = CreateTestDeltaResult();
            var preflight = CreateTestPreflight();

            var content = _commandProvider.GetRefactorWithDeltaResultPayload(TestFileName, TestFileContent, TestCachePath, deltaResult, preflight);

            var expectedNewScore = deltaResult.NewScore.ToString("0.0", CultureInfo.InvariantCulture);
            var expectedOldScore = deltaResult.OldScore.ToString("0.0", CultureInfo.InvariantCulture);
            Assert.AreEqual($"{{\"delta-result\":{{\"new-score\":{expectedNewScore},\"old-score\":{expectedOldScore}}},\"file-name\":\"{TestFileName}\",\"file-content\":\"{TestFileContent}\",\"preflight\":{{\"file-types\":[\"{preflight.FileTypes.First()}\"]}},\"cache-path\":\"{TestCachePath}\"}}", content);
        }

        [DataTestMethod]
        [DataRow(false, "refactor preflight")]
        [DataRow(true, "refactor preflight --force")]
        public void GetPreflightSupportInformationCommand_ReturnsCorrectCommand(bool force, string expected)
        {
            var command = _commandProvider.GetPreflightSupportInformationCommand(force);

            Assert.AreEqual(expected, command);
        }

        //[TestMethod]
        //public void GetRefactorPostCommand_With_Skip_Cache()
        //{
        //    // ARRANGE
        //    var provider = new CliCommandProvider();
        //    var fnToRefactorJson = "function(){};";

        //    // ACT
        //    var command = provider.GetRefactorPostCommand(skipCache: true, fnToRefactor: fnToRefactorJson);

        //    // ASSERT
        //    Assert.AreEqual(command, $"refactor post --skip-cache --fn-to-refactor \"{fnToRefactorJson}\"");
        //}

        //[TestMethod]
        //public void GetRefactorPostCommand_Withiout_Skip_Cache()
        //{
        //    // ARRANGE
        //    var provider = new CliCommandProvider();
        //    var fnToRefactorJson = "function(){};";

        //    // ACT
        //    var command = provider.GetRefactorPostCommand(skipCache: false, fnToRefactor: fnToRefactorJson);

        //    // ASSERT
        //    Assert.AreEqual(command, $"refactor post --fn-to-refactor \"{fnToRefactorJson}\"");
        //}

        //[TestMethod]
        //public void GetRefactorPostCommand_Withiout_Skip_Cache_Use_Staging()
        //{
        //    // ARRANGE
        //    var provider = new CliCommandProvider();
        //    var fnToRefactorJson = "function(){};";

        //    // ACT
        //    var command = provider.GetRefactorPostCommand(skipCache: false, fnToRefactor: fnToRefactorJson);

        //    // ASSERT
        //    Assert.AreEqual(command, $"refactor post --fn-to-refactor \"{fnToRefactorJson}\"");
        //}

        //[TestMethod]
        //public void GetRefactorPostCommand_Withiout_Skip_Cache_With_Token()
        //{
        //    // ARRANGE
        //    var provider = new CliCommandProvider();
        //    var fnToRefactorJson = "function(){};";
        //    var token = "ABC";

        //    // ACT
        //    var command = provider.GetRefactorPostCommand(skipCache: false, fnToRefactor: fnToRefactorJson, token: token);

        //    // ASSERT
        //    Assert.AreEqual(command, $"refactor post --fn-to-refactor {fnToRefactorJson} --token {token}");
        //}
    }
}
