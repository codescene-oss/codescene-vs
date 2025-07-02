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
    }
}
