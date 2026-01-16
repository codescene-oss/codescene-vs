using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class CliCommandProviderTests
    {
        private CliCommandProvider _commandProvider;
        private Mock<ILogger> _mockLogger;
        [TestInitialize]
        public void Initialize()
        {
            _mockLogger = new Mock<ILogger>();
            _commandProvider = new CliCommandProvider(new CliObjectScoreCreator(_mockLogger.Object));
        }

        [TestMethod]
        public void VersionCommand_ShouldReturnCorrectString()
        {
            // ACT
            var command = _commandProvider.VersionCommand;

            // ASSERT
            Assert.AreEqual("version --sha", command,
                "VersionCommand should return 'version --sha'.");
        }

        [TestMethod]
        public void GetReviewFileContentCommand_ShouldIncludeIdeApiAndFilename()
        {
            // ARRANGE
            var testPath = "testfile.txt";

            // ACT
            var command = _commandProvider.GetReviewFileContentCommand(testPath);

            // ASSERT
            Assert.AreEqual("review --file-name testfile.txt", command, "GetReviewFileContentCommand didn't return the expected string.");
        }

        [TestMethod]
        public void GetReviewPathCommand_ShouldIncludeIdeApiAndPath()
        {
            // ARRANGE
            var testPath = "some/path";

            // ACT
            var command = _commandProvider.GetReviewPathCommand(testPath);

            // ASSERT
            Assert.AreEqual("review some/path", command, "GetReviewPathCommand didn't return the expected string.");
        }

        [TestMethod]
        public void GetReviewFileContentPayload()
        {
            // ARRANGE
            var filePath = "js";
            var fileContent = "content";
            var cachePath = "/home/user/cache";

            // ACT
            var command = _commandProvider.GetReviewFileContentPayload(filePath, fileContent, cachePath);

            // ASSERT
            Assert.AreEqual($"{{\"path\":\"{filePath}\",\"file-content\":\"{fileContent}\",\"cache-path\":\"{cachePath}\"}}", command);
        }

        [TestMethod]
        public void GetRefactorWithCodeSmellsPayload_Without_Preflight_Parameter()
        {
            // ARRANGE
            var fileName = "js";
            var fileContent = "content";
            var cachePath = "/home/user/cache";
            var codesmells = new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "test" } };

            // ACT
            var content = _commandProvider.GetRefactorWithCodeSmellsPayload(fileName, fileContent, cachePath, codesmells, null);

            // ASSERT
            Assert.AreEqual(content, $"{{\"code-smells\":[{{\"category\":\"{codesmells.First().Category}\"}}],\"file-name\":\"{fileName}\",\"file-content\":\"{fileContent}\",\"cache-path\":\"{cachePath}\"}}");
        }

        [TestMethod]
        public void GetRefactorWithCodeSmellsPayload_With_Preflight_Parameter()
        {
            // ARRANGE
            var fileName = "js";
            var fileContent = "content";
            var cachePath = "/home/user/cache";
            var codesmells = new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "test" } };
            var preflight = new PreFlightResponseModel { FileTypes = new string[] { ".js" } };

            // ACT
            var content = _commandProvider.GetRefactorWithCodeSmellsPayload(fileName, fileContent, cachePath, codesmells, preflight);


            // ASSERT
            Assert.AreEqual(content, $"{{\"code-smells\":[{{\"category\":\"{codesmells.First().Category}\"}}],\"file-name\":\"{fileName}\",\"file-content\":\"{fileContent}\",\"preflight\":{{\"file-types\":[\"{preflight.FileTypes.First()}\"]}},\"cache-path\":\"{cachePath}\"}}");
        }

        [TestMethod]
        public void GetRefactorWithDeltaResultPayload_Without_Preflight_Parameter()
        {
            // ARRANGE
            var fileName = "js";
            var fileContent = "content";
            var cachePath = "/home/user/cache";
            var deltaResult = new DeltaResponseModel { NewScore = 2, OldScore = 3 };
            var codesmells = new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "test" } };

            // ACT
            var content = _commandProvider.GetRefactorWithDeltaResultPayload(fileName, fileContent, cachePath, deltaResult, null);

            // ASSERT
            Assert.AreEqual(content, $"{{\"delta-result\":{{\"new-score\":{deltaResult.NewScore.ToString("0.0", CultureInfo.InvariantCulture)},\"old-score\":{deltaResult.OldScore.ToString("0.0", CultureInfo.InvariantCulture)}}},\"file-name\":\"{fileName}\",\"file-content\":\"{fileContent}\",\"cache-path\":\"{cachePath}\"}}");
        }

        [TestMethod]
        public void GetRefactorWithDeltaResultPayload_With_Preflight_Parameter()
        {
            // ARRANGE
            var fileName = "js";
            var fileContent = "content";
            var cachePath = "/home/user/cache";
            var deltaResult = new DeltaResponseModel { NewScore = 2, OldScore = 3 };
            var preflight = new PreFlightResponseModel { FileTypes = new string[] { ".js" } };

            // ACT
            var content = _commandProvider.GetRefactorWithDeltaResultPayload(fileName, fileContent, cachePath, deltaResult, preflight);


            // ASSERT
            Assert.AreEqual(content, $"{{\"delta-result\":{{\"new-score\":{deltaResult.NewScore.ToString("0.0", CultureInfo.InvariantCulture)},\"old-score\":{deltaResult.OldScore.ToString("0.0", CultureInfo.InvariantCulture)}}},\"file-name\":\"{fileName}\",\"file-content\":\"{fileContent}\",\"preflight\":{{\"file-types\":[\"{preflight.FileTypes.First()}\"]}},\"cache-path\":\"{cachePath}\"}}");
        }


        [TestMethod]
        public void GetPreflightSupportInformationCommand_Without_Force_Parameter()
        {
            // ACT
            var command = _commandProvider.GetPreflightSupportInformationCommand(force: false);

            // ASSERT
            Assert.AreEqual(command, "refactor preflight");
        }

        [TestMethod]
        public void GetPreflightSupportInformationCommand_With_Force_Parameter()
        {
            // ACT
            var command = _commandProvider.GetPreflightSupportInformationCommand(force: true);

            // ASSERT
            Assert.AreEqual(command, "refactor preflight --force");
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
