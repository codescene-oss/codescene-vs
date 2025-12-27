using Codescene.VSExtension.Core.Application.Services.Cli;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class CliCommandProviderTests
    {
        [TestMethod]
        public void VersionCommand_ShouldReturnCorrectString()
        {
            // ARRANGE
            var provider = new CliCommandProvider();

            // ACT
            var command = provider.VersionCommand;

            // ASSERT
            Assert.AreEqual("version --sha", command,
                "VersionCommand should return 'version --sha'.");
        }

        [TestMethod]
        public void GetReviewFileContentCommand_ShouldIncludeIdeApiAndFilename()
        {
            // ARRANGE
            var provider = new CliCommandProvider();
            var testPath = "testfile.txt";

            // ACT
            var command = provider.GetReviewFileContentCommand(testPath);

            // ASSERT
            Assert.AreEqual("review --file-name testfile.txt", command, "GetReviewFileContentCommand didn't return the expected string.");
        }

        [TestMethod]
        public void GetReviewPathCommand_ShouldIncludeIdeApiAndPath()
        {
            // ARRANGE
            var provider = new CliCommandProvider();
            var testPath = "some/path";

            // ACT
            var command = provider.GetReviewPathCommand(testPath);

            // ASSERT
            Assert.AreEqual("review some/path", command, "GetReviewPathCommand didn't return the expected string.");
        }

        [TestMethod]
        public void GetRefactorCommandWithCodeSmells_Without_Preflight_Parameter()
        {
            // ARRANGE
            var extension = "js";
            var codeSmellsJson = "{ id = 1 }";
            var provider = new CliCommandProvider();

            // ACT
            var command = provider.GetRefactorCommandWithCodeSmells(extension, codeSmellsJson);

            // ASSERT
            Assert.AreEqual(command, $"refactor fns-to-refactor --file-name {extension} --code-smells \"{codeSmellsJson}\"");
        }

        [TestMethod]
        public void GetRefactorCommandWithCodeSmells_With_Preflight_Parameter()
        {
            // ARRANGE
            var extension = "js";
            var codeSmellsJson = "{ id = 1 }";
            var preflight = "{ id = 5 }";
            var provider = new CliCommandProvider();

            // ACT
            var command = provider.GetRefactorCommandWithCodeSmells(extension, codeSmellsJson, preflight);

            // ASSERT
            Assert.AreEqual(command, $"refactor fns-to-refactor --file-name {extension} --preflight \"{preflight}\" --code-smells \"{codeSmellsJson}\"");
        }

        [TestMethod]
        public void GetRefactorCommandWithDeltaResult_Without_Preflight_Parameter()
        {
            // ARRANGE
            var extension = "js";
            var deltaresult = "{ id = 1 }";
            var provider = new CliCommandProvider();

            // ACT
            var command = provider.GetRefactorCommandWithDeltaResult(extension, deltaresult);

            // ASSERT
            Assert.AreEqual(command, $"refactor fns-to-refactor --extension {extension} --delta-result \"{deltaresult}\"");
        }

        [TestMethod]
        public void GetRefactorCommandWithDeltaResult_With_Preflight_Parameter()
        {
            // ARRANGE
            var extension = "js";
            var deltaresult = "{ id = 1 }";
            var preflight = "{ id = 5 }";
            var provider = new CliCommandProvider();

            // ACT
            var command = provider.GetRefactorCommandWithDeltaResult(extension, deltaresult, preflight);

            // ASSERT
            Assert.AreEqual(command, $"refactor fns-to-refactor --extension {extension} --preflight \"{preflight}\" --delta-result \"{deltaresult}\"");
        }


        [TestMethod]
        public void GetPreflightSupportInformationCommand_Without_Force_Parameter()
        {
            // ARRANGE
            var provider = new CliCommandProvider();

            // ACT
            var command = provider.GetPreflightSupportInformationCommand(force: false);

            // ASSERT
            Assert.AreEqual(command, "refactor preflight");
        }

        [TestMethod]
        public void GetPreflightSupportInformationCommand_With_Force_Parameter()
        {
            // ARRANGE
            var provider = new CliCommandProvider();

            // ACT
            var command = provider.GetPreflightSupportInformationCommand(force: true);

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
