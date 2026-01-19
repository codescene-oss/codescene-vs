using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class SerializationTests
    {
        #region Helper Methods

        private static T DeserializeJson<T>(string json)
        {
            var result = JsonConvert.DeserializeObject<T>(json);
            Assert.IsNotNull(result, $"Deserialization of {typeof(T).Name} returned null");
            return result;
        }

        private static void AssertRange(CliRangeModel range, int startLine, int startCol, int endLine, int endCol)
        {
            Assert.AreEqual(startLine, range.Startline, "StartLine mismatch");
            Assert.AreEqual(startCol, range.StartColumn, "StartColumn mismatch");
            Assert.AreEqual(endLine, range.EndLine, "EndLine mismatch");
            Assert.AreEqual(endCol, range.EndColumn, "EndColumn mismatch");
        }

        private static void AssertJsonContainsProperties(string json, params string[] properties)
        {
            foreach (var prop in properties)
            {
                Assert.IsTrue(json.Contains($"\"{prop}\""), $"JSON should contain property '{prop}'");
            }
        }

        #endregion

        #region CliReviewModel Tests

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

            Assert.AreEqual(8.5f, result.Score);
            Assert.AreEqual("abc123", result.RawScore);
            Assert.AreEqual("Large File", result.FileLevelCodeSmells[0].Category);
            Assert.AreEqual("ProcessData", result.FunctionLevelCodeSmells[0].Function);
        }

        [TestMethod]
        public void CliReviewModel_Deserialize_WithNullScore()
        {
            var result = DeserializeJson<CliReviewModel>(@"{""score"": null}");
            Assert.IsNull(result.Score);
        }

        #endregion

        #region DeltaResponseModel Tests

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

            Assert.AreEqual((decimal)-0.5f, result.ScoreChange);
            Assert.AreEqual((decimal)8.0f, result.OldScore);
            Assert.AreEqual((decimal)7.5f, result.NewScore);
            Assert.AreEqual("Calculate", result.FunctionLevelFindings[0].Function.Name);
        }

        #endregion

        #region PreFlightResponseModel Tests

        [TestMethod]
        public void PreFlightResponseModel_Deserialize_WithFileTypes()
        {
            var result = DeserializeJson<PreFlightResponseModel>(@"{ ""file-types"": ["".cs"", "".js"", "".py""] }");

            CollectionAssert.AreEqual(new[] { ".cs", ".js", ".py" }, result.FileTypes);
        }

        #endregion

        #region FnToRefactorModel Tests

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

            Assert.AreEqual("ProcessOrder", result.Name);
            Assert.AreEqual("function body here", result.Body);
            Assert.AreEqual("cs", result.FileType);
            Assert.AreEqual("base64data", result.NippyB64);
            AssertRange(result.Range, 10, 5, 50, 5);
        }

        [TestMethod]
        public void FnToRefactorModel_Serialize_RoundTrip_PreservesData()
        {
            var model = new FnToRefactorModel
            {
                Name = "TestFunction",
                Body = "code",
                FileType = "js",
                Range = new CliRangeModel { Startline = 1, StartColumn = 1, EndLine = 10, EndColumn = 1 }
            };

            var json = JsonConvert.SerializeObject(model);
            var deserialized = JsonConvert.DeserializeObject<FnToRefactorModel>(json);

            Assert.AreEqual(model.Name, deserialized.Name);
            Assert.AreEqual(model.Body, deserialized.Body);
            Assert.AreEqual(model.FileType, deserialized.FileType);
        }

        #endregion

        #region ReviewRequestModel Tests

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

        #endregion

        #region CliRangeModel Tests

        [TestMethod]
        public void CliRangeModel_Deserialize_WithKebabCaseProperties()
        {
            var json = @"{ ""start-line"": 10, ""start-column"": 5, ""end-line"": 20, ""end-column"": 15 }";

            var result = DeserializeJson<CliRangeModel>(json);

            AssertRange(result, 10, 5, 20, 15);
        }

        [TestMethod]
        public void CliRangeModel_Serialize_UsesKebabCaseProperties()
        {
            var model = new CliRangeModel { Startline = 1, StartColumn = 1, EndLine = 100, EndColumn = 50 };

            var json = JsonConvert.SerializeObject(model);

            AssertJsonContainsProperties(json, "start-line", "start-column", "end-line", "end-column");
        }

        #endregion

        #region CliCodeSmellModel Tests

        [TestMethod]
        public void CliCodeSmellModel_Deserialize_WithHighlightRange()
        {
            var json = @"{
                ""category"": ""Deep Nesting"",
                ""details"": ""Depth: 5"",
                ""highlight-range"": { ""start-line"": 15, ""start-column"": 9, ""end-line"": 25, ""end-column"": 9 }
            }";

            var result = DeserializeJson<CliCodeSmellModel>(json);

            Assert.AreEqual("Deep Nesting", result.Category);
            Assert.AreEqual("Depth: 5", result.Details);
            Assert.IsNotNull(result.Range);
            Assert.AreEqual(15, result.Range.Startline);
        }

        #endregion

        #region FnsToRefactorCodeSmellRequestModel Tests

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
                Preflight = null
            };

            var json = JsonConvert.SerializeObject(model, settings);

            Assert.IsFalse(json.Contains("\"preflight\""), "Null preflight should not appear in JSON");
            AssertJsonContainsProperties(json, "file-name", "file-content", "code-smells");
        }

        #endregion
    }
}
