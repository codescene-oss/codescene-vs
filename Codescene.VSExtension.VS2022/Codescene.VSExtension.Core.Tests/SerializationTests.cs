// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class SerializationTests
    {
        private static T DeserializeJson<T>(string json)
        {
            var result = JsonConvert.DeserializeObject<T>(json);
            Assert.IsNotNull(result, $"Deserialization of {typeof(T).Name} returned null");
            return result;
        }

        private static void AssertRangeEquals(CliRangeModel actual, CliRangeModel expected)
        {
            AssertStartPosition(actual, expected);
            AssertEndPosition(actual, expected);
        }

        private static void AssertStartPosition(CliRangeModel actual, CliRangeModel expected)
        {
            Assert.AreEqual(expected.StartLine, actual.StartLine, "StartLine mismatch");
            Assert.AreEqual(expected.StartColumn, actual.StartColumn, "StartColumn mismatch");
        }

        private static void AssertEndPosition(CliRangeModel actual, CliRangeModel expected)
        {
            Assert.AreEqual(expected.EndLine, actual.EndLine, "EndLine mismatch");
            Assert.AreEqual(expected.EndColumn, actual.EndColumn, "EndColumn mismatch");
        }

        private static CliRangeModel CreateExpectedRange(int startLine, int startCol, int endLine, int endCol) =>
            new CliRangeModel { StartLine = startLine, StartColumn = startCol, EndLine = endLine, EndColumn = endCol };

        private static void AssertJsonContainsProperties(string json, params string[] properties)
        {
            foreach (var prop in properties)
            {
                Assert.IsTrue(json.Contains($"\"{prop}\""), $"JSON should contain property '{prop}'");
            }
        }

        [TestMethod]
        public void CliReviewModel_Deserialize_WithAllFields()
        {
            var json = @"{
                ""score"": 8.5,
                ""raw-score"": ""abc123"",
                ""file-level-code-smells"": [{ ""category"": ""Large File"", ""details"": ""500 lines"" }],
                ""function-level-code-smells"": [{ ""function"": ""ProcessData"", ""code-smells"": [{ ""category"": ""Complex Method"" }] }]
            }";

            var result = DeserializeJson<CliReviewModel>(json);

            AssertCliReviewModelFields(result);
        }

        private static void AssertCliReviewModelFields(CliReviewModel result)
        {
            AssertCliReviewScores(result);
            AssertCliReviewCodeSmells(result);
        }

        private static void AssertCliReviewScores(CliReviewModel result)
        {
            Assert.AreEqual(8.5f, result.Score);
            Assert.AreEqual("abc123", result.RawScore);
        }

        private static void AssertCliReviewCodeSmells(CliReviewModel result)
        {
            Assert.AreEqual("Large File", result.FileLevelCodeSmells[0].Category);
            Assert.AreEqual("ProcessData", result.FunctionLevelCodeSmells[0].Function);
        }

        [TestMethod]
        public void CliReviewModel_Deserialize_WithNullScore()
        {
            var result = DeserializeJson<CliReviewModel>(@"{""score"": null}");
            Assert.IsNull(result.Score);
        }

        [TestMethod]
        public void DeltaResponseModel_Deserialize_WithScoreChange()
        {
            var json = @"{
                ""score-change"": -0.5,
                ""old-score"": 8.0,
                ""new-score"": 7.5,
                ""function-level-findings"": [{
                    ""function"": { ""name"": ""Calculate"", ""range"": { ""start-line"": 10, ""end-line"": 30 } }
                }]
            }";

            var result = DeserializeJson<DeltaResponseModel>(json);

            AssertDeltaScoreFields(result, expectedScoreChange: -0.5m, expectedOldScore: 8.0m, expectedNewScore: 7.5m);
            Assert.AreEqual("Calculate", result.FunctionLevelFindings[0].Function.Name);
        }

        private static void AssertDeltaScoreFields(DeltaResponseModel result, decimal expectedScoreChange, decimal expectedOldScore, decimal expectedNewScore)
        {
            Assert.AreEqual(expectedScoreChange, result.ScoreChange);
            Assert.AreEqual(expectedOldScore, result.OldScore);
            Assert.AreEqual(expectedNewScore, result.NewScore);
        }

        [TestMethod]
        public void PreFlightResponseModel_Deserialize_WithFileTypes()
        {
            var result = DeserializeJson<PreFlightResponseModel>(@"{ ""file-types"": ["".cs"", "".js"", "".py""] }");

            CollectionAssert.AreEqual(new[] { ".cs", ".js", ".py" }, result.FileTypes);
        }

        [TestMethod]
        public void FnToRefactorModel_Deserialize_WithAllFields()
        {
            var json = @"{
                ""name"": ""ProcessOrder"",
                ""body"": ""function body here"",
                ""file-type"": ""cs"",
                ""nippy-b64"": ""base64data"",
                ""range"": { ""start-line"": 10, ""start-column"": 5, ""end-line"": 50, ""end-column"": 5 }
            }";

            var result = DeserializeJson<FnToRefactorModel>(json);

            var expected = CreateExpectedFnToRefactor("ProcessOrder", "function body here", "cs", "base64data");
            AssertFnToRefactorModelEquals(result, expected);
            AssertRangeEquals(result.Range, CreateExpectedRange(10, 5, 50, 5));
        }

        private static FnToRefactorModel CreateExpectedFnToRefactor(string name, string body, string fileType, string nippyB64) =>
            new FnToRefactorModel { Name = name, Body = body, FileType = fileType, NippyB64 = nippyB64 };

        private static void AssertFnToRefactorModelEquals(FnToRefactorModel actual, FnToRefactorModel expected)
        {
            AssertFnIdentity(actual, expected);
            AssertFnContent(actual, expected);
        }

        private static void AssertFnIdentity(FnToRefactorModel actual, FnToRefactorModel expected)
        {
            Assert.AreEqual(expected.Name, actual.Name);
            Assert.AreEqual(expected.FileType, actual.FileType);
        }

        private static void AssertFnContent(FnToRefactorModel actual, FnToRefactorModel expected)
        {
            Assert.AreEqual(expected.Body, actual.Body);
            Assert.AreEqual(expected.NippyB64, actual.NippyB64);
        }

        [TestMethod]
        public void FnToRefactorModel_Serialize_RoundTrip_PreservesData()
        {
            var model = new FnToRefactorModel
            {
                Name = "TestFunction",
                Body = "code",
                FileType = "js",
                Range = new CliRangeModel { StartLine = 1, StartColumn = 1, EndLine = 10, EndColumn = 1 },
            };

            var json = JsonConvert.SerializeObject(model);
            var deserialized = JsonConvert.DeserializeObject<FnToRefactorModel>(json);

            AssertFnToRefactorRoundTrip(model, deserialized);
        }

        private static void AssertFnToRefactorRoundTrip(FnToRefactorModel original, FnToRefactorModel deserialized)
        {
            Assert.AreEqual(original.Name, deserialized.Name);
            Assert.AreEqual(original.Body, deserialized.Body);
            Assert.AreEqual(original.FileType, deserialized.FileType);
        }

        [TestMethod]
        public void ReviewRequestModel_Serialize_ProducesCorrectPropertyNames()
        {
            var model = new ReviewRequestModel { FilePath = "test.cs", FileContent = "public class Test {}", CachePath = "/cache/path" };

            var json = JsonConvert.SerializeObject(model);

            AssertJsonContainsProperties(json, "path", "file-content", "cache-path");
        }

        [TestMethod]
        public void ReviewRequestModel_RoundTrip_PreservesData()
        {
            var model = new ReviewRequestModel { FilePath = "test.cs", FileContent = "code content", CachePath = "/cache" };

            var json = JsonConvert.SerializeObject(model);
            var deserialized = JsonConvert.DeserializeObject<ReviewRequestModel>(json);

            Assert.AreEqual(model.FilePath, deserialized.FilePath);
            Assert.AreEqual(model.FileContent, deserialized.FileContent);
            Assert.AreEqual(model.CachePath, deserialized.CachePath);
        }

        [TestMethod]
        public void CliRangeModel_Deserialize_WithKebabCaseProperties()
        {
            var json = @"{ ""start-line"": 10, ""start-column"": 5, ""end-line"": 20, ""end-column"": 15 }";

            var result = DeserializeJson<CliRangeModel>(json);

            AssertRangeEquals(result, CreateExpectedRange(10, 5, 20, 15));
        }

        [TestMethod]
        public void CliRangeModel_Serialize_UsesKebabCaseProperties()
        {
            var model = new CliRangeModel { StartLine = 1, StartColumn = 1, EndLine = 100, EndColumn = 50 };

            var json = JsonConvert.SerializeObject(model);

            AssertJsonContainsProperties(json, "start-line", "start-column", "end-line", "end-column");
        }

        [TestMethod]
        public void CliCodeSmellModel_Deserialize_WithHighlightRange()
        {
            var json = @"{
                ""category"": ""Deep Nesting"",
                ""details"": ""Depth: 5"",
                ""highlight-range"": { ""start-line"": 15, ""start-column"": 9, ""end-line"": 25, ""end-column"": 9 }
            }";

            var result = DeserializeJson<CliCodeSmellModel>(json);

            AssertCodeSmellFields(result, expectedCategory: "Deep Nesting", expectedDetails: "Depth: 5", expectedRangeStartLine: 15);
        }

        private static void AssertCodeSmellFields(CliCodeSmellModel result, string expectedCategory, string expectedDetails, int expectedRangeStartLine)
        {
            AssertCodeSmellMetadata(result, expectedCategory, expectedDetails);
            AssertCodeSmellRange(result, expectedRangeStartLine);
        }

        private static void AssertCodeSmellMetadata(CliCodeSmellModel result, string expectedCategory, string expectedDetails)
        {
            Assert.AreEqual(expectedCategory, result.Category);
            Assert.AreEqual(expectedDetails, result.Details);
        }

        private static void AssertCodeSmellRange(CliCodeSmellModel result, int expectedRangeStartLine)
        {
            Assert.IsNotNull(result.Range);
            Assert.AreEqual(expectedRangeStartLine, result.Range.StartLine);
        }

        [TestMethod]
        public void FnsToRefactorCodeSmellRequestModel_Serialize_IgnoresDefaultValues()
        {
            var settings = new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore };
            var model = new FnsToRefactorCodeSmellRequestModel
            {
                FileName = "test.cs",
                FileContent = "code",
                CachePath = "/cache",
                CodeSmells = new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "Test" } },
                Preflight = null,
            };

            var json = JsonConvert.SerializeObject(model, settings);

            Assert.IsFalse(json.Contains("\"preflight\""), "Null preflight should not appear in JSON");
            AssertJsonContainsProperties(json, "file-name", "file-content", "code-smells");
        }
    }
}
