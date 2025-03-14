using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.ReviewResult;
using Codescene.VSExtension.Core.Models.ReviewResultModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class CliExecuterTests
    {
        // Dummy implementations for the interfaces used by CliExecuter

        // Fake command provider returns predetermined command arguments.
        private class FakeCliCommandProvider : ICliCommandProvider
        {
            // Used for GetFileVersion
            public string VersionCommand { get; set; }
            // Delegates for review commands.
            public Func<string, string> GetReviewPathCommandDelegate { get; set; }
            public Func<string, string> GetReviewFileContentCommandDelegate { get; set; }

            public string GetReviewPathCommand(string path)
            {
                return GetReviewPathCommandDelegate != null ? GetReviewPathCommandDelegate(path) : string.Empty;
            }

            public string GetReviewFileContentCommand(string path)
            {
                return GetReviewFileContentCommandDelegate != null ? GetReviewFileContentCommandDelegate(path) : string.Empty;
            }
        }

        // Fake model mapper that converts a ReviewResultModel into a ReviewMapModel.
        private class FakeModelMapper : IModelMapper
        {
            public ReviewMapModel Map(ReviewResultModel model)
            {
                // For testing purposes, we simulate a mapping that creates a review object
                // with non-empty ExpressionLevel and FunctionLevel lists.
                return new ReviewMapModel
                {
                    // In a real scenario the mapper would use data from the model.
                    ExpressionLevel = new List<ReviewModel> { new ReviewModel { Path = "expr1" } },
                    FunctionLevel = new List<ReviewModel> { new ReviewModel { Path = "func1" } }
                };
            }

            public IEnumerable<ReviewModel> Map(CsReview result)
            {
                throw new NotImplementedException();
            }

            public IEnumerable<ReviewModel> MapToList(ReviewResultModel result)
            {
                throw new NotImplementedException();
            }
        }

        // Fake settings provider allows overriding the CLI file path.
        private class FakeCliSettingsProvider : ICliSettingsProvider
        {
            public string RequiredDevToolVersion { get; set; } = "3b28b97d2f4a17d596c6f2ec5cf2e86363c08d21";
            public string CliArtifactName { get; set; } = "artifact.zip";
            public string CliArtifactUrl { get; set; } = "dummy";
            public string CliFileName { get; set; } = "cs-ide.exe";
            public string ArtifactBaseUrl { get; set; } = "dummy";
            public string CliFileFullPath { get; set; }
        }

        // Helper to obtain the full path to cmd.exe.
        private string GetCmdExePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        }

        // Before each test, clear the static ActiveReviewList in CliExecuter.
        [TestInitialize]
        public void TestInitialize()
        {
            var field = typeof(CliExecuter).GetField("ActiveReviewList", BindingFlags.Static | BindingFlags.NonPublic);
            var dict = field.GetValue(null) as IDictionary<string, ReviewMapModel>;
            dict.Clear();
        }

        // Test for GetFileVersion.
        [TestMethod]
        public void TestGetFileVersion_ShouldReturnVersion()
        {
            // ARRANGE: Set up the fake command provider to echo a version string.
            var fakeCommandProvider = new FakeCliCommandProvider
            {
                VersionCommand = "/c echo 1.0.0"
            };

            var fakeMapper = new FakeModelMapper();
            var fakeSettings = new FakeCliSettingsProvider
            {
                // Use cmd.exe as our executable so that the echo command works.
                CliFileFullPath = GetCmdExePath()
            };

            var cliExecuter = new CliExecuter(fakeCommandProvider, fakeMapper, fakeSettings);

            // ACT: Call GetFileVersion, which executes the command.
            var version = cliExecuter.GetFileVersion();

            // ASSERT: The command should echo "1.0.0" (trimmed of any newline characters).
            Assert.AreEqual("1.0.0", version);
        }

        // Test for Review(string path) when no file content is provided.
        [TestMethod]
        public void TestReview_WithPath_ShouldReturnMappedReview()
        {
            // ARRANGE: Configure the fake command provider to echo a JSON string.
            var fakeCommandProvider = new FakeCliCommandProvider
            {
                GetReviewPathCommandDelegate = (path) => "/c echo {\"Dummy\":\"test\"}"
            };

            var fakeMapper = new FakeModelMapper();
            var fakeSettings = new FakeCliSettingsProvider
            {
                CliFileFullPath = GetCmdExePath()
            };

            var cliExecuter = new CliExecuter(fakeCommandProvider, fakeMapper, fakeSettings);

            // ACT: Execute the Review method.
            var review = cliExecuter.Review("dummyPath");

            // ASSERT: Verify that the review is mapped and contains non-null tag lists.
            Assert.IsNotNull(review, "Review should not be null.");
            Assert.IsNotNull(review.ExpressionLevel, "ExpressionLevel should not be null.");
            Assert.IsNotNull(review.FunctionLevel, "FunctionLevel should not be null.");
        }

        // Test for Review(string path, string content) when file content is provided.
        [TestMethod]
        public void TestReview_WithPathAndContent_ShouldReturnMappedReview()
        {
            // ARRANGE: Configure the fake command provider to echo a different JSON string.
            var fakeCommandProvider = new FakeCliCommandProvider
            {
                GetReviewFileContentCommandDelegate = (path) => "/c echo {\"Dummy\":\"test2\"}"
            };

            var fakeMapper = new FakeModelMapper();
            var fakeSettings = new FakeCliSettingsProvider
            {
                CliFileFullPath = GetCmdExePath()
            };

            var cliExecuter = new CliExecuter(fakeCommandProvider, fakeMapper, fakeSettings);

            // ACT: Call the Review method that accepts content.
            var review = cliExecuter.Review("dummyPath", "some content");

            // ASSERT: Verify that the review object is created and mapped.
            Assert.IsNotNull(review, "Review should not be null.");
            Assert.IsNotNull(review.ExpressionLevel, "ExpressionLevel should not be null.");
            Assert.IsNotNull(review.FunctionLevel, "FunctionLevel should not be null.");
        }

        // Test active review list management: adding, retrieving, and removing reviews.
        [TestMethod]
        public void TestAddAndRemoveFromActiveReviewList_ShouldManageReviewList()
        {
            // ARRANGE: Configure fake dependencies.
            var fakeCommandProvider = new FakeCliCommandProvider
            {
                GetReviewPathCommandDelegate = (path) => "/c echo {\"Dummy\":\"test\"}"
            };
            var fakeMapper = new FakeModelMapper();
            var fakeSettings = new FakeCliSettingsProvider
            {
                CliFileFullPath = GetCmdExePath()
            };

            var cliExecuter = new CliExecuter(fakeCommandProvider, fakeMapper, fakeSettings);

            // ACT: Add a review to the active list.
            cliExecuter.AddToActiveReviewList("file1");
            var review1 = cliExecuter.GetReviewObject("file1");

            // ASSERT: The review should exist.
            Assert.IsNotNull(review1, "Review should be added and retrieved.");

            // Remove it and then retrieve again (which re-adds it).
            cliExecuter.RemoveFromActiveReviewList("file1");
            var reviewAfterRemove = cliExecuter.GetReviewObject("file1");

            // The review object should be re-created.
            Assert.IsNotNull(reviewAfterRemove, "Review should be re-created when not found in the active list.");
        }

        // Test GetTaggerItems, which combines two lists from the review.
        [TestMethod]
        public void TestGetTaggerItems_ShouldReturnCombinedList()
        {
            // ARRANGE: Set up the fake command provider to echo a JSON string.
            var fakeCommandProvider = new FakeCliCommandProvider
            {
                GetReviewPathCommandDelegate = (path) => "/c echo {\"Dummy\":\"test\"}"
            };
            var fakeMapper = new FakeModelMapper();
            var fakeSettings = new FakeCliSettingsProvider
            {
                CliFileFullPath = GetCmdExePath()
            };

            var cliExecuter = new CliExecuter(fakeCommandProvider, fakeMapper, fakeSettings);

            // Ensure a review is added for "file2".
            var review = cliExecuter.GetReviewObject("file2");

            // ACT: Get tagger items, which is a concatenation of ExpressionLevel and FunctionLevel.
            var taggerItems = cliExecuter.GetTaggerItems("file2");

            // ASSERT: In our fake mapping, each list has one item (total of 2).
            Assert.AreEqual(2, taggerItems.Count, "There should be 2 tagger items (1 from each list).");
        }
    }
}
