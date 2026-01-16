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
        #region CliReviewModel Tests

        [TestMethod]
        public void CliReviewModel_Deserialize_WithAllFields()
        {
            // Arrange
            var json = @"{
                ""score"": 8.5,
                ""raw-score"": ""abc123"",
                ""file-level-code-smells"": [
                    {
                        ""category"": ""Large File"",
                        ""details"": ""500 lines"",
                        ""highlight-range"": {
                            ""start-line"": 1,
                            ""start-column"": 1,
                            ""end-line"": 500,
                            ""end-column"": 1
                        }
                    }
                ],
                ""function-level-code-smells"": [
                    {
                        ""function"": ""ProcessData"",
                        ""range"": {
                            ""start-line"": 10,
                            ""start-column"": 5,
                            ""end-line"": 50,
                            ""end-column"": 5
                        },
                        ""code-smells"": [
                            {
                                ""category"": ""Complex Method"",
                                ""details"": ""CC: 15""
                            }
                        ]
                    }
                ]
            }";

            // Act
            var result = JsonConvert.DeserializeObject<CliReviewModel>(json);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(8.5f, result.Score);
            Assert.AreEqual("abc123", result.RawScore);
            Assert.AreEqual(1, result.FileLevelCodeSmells.Count);
            Assert.AreEqual("Large File", result.FileLevelCodeSmells[0].Category);
            Assert.AreEqual(1, result.FunctionLevelCodeSmells.Count);
            Assert.AreEqual("ProcessData", result.FunctionLevelCodeSmells[0].Function);
        }

        [TestMethod]
        public void CliReviewModel_Deserialize_WithNullScore()
        {
            // Arrange
            var json = @"{""score"": null}";

            // Act
            var result = JsonConvert.DeserializeObject<CliReviewModel>(json);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNull(result.Score);
        }

        #endregion

        #region DeltaResponseModel Tests

        [TestMethod]
        public void DeltaResponseModel_Deserialize_WithScoreChange()
        {
            // Arrange
            var json = @"{
                ""score-change"": -0.5,
                ""old-score"": 8.0,
                ""new-score"": 7.5,
                ""function-level-findings"": [
                    {
                        ""function"": {
                            ""name"": ""Calculate"",
                            ""range"": {
                                ""start-line"": 10,
                                ""end-line"": 30
                            }
                        },
                        ""change-details"": [
                            {
                                ""line"": 15,
                                ""description"": ""Increased complexity"",
                                ""change-type"": ""degraded"",
                                ""category"": ""Complex Conditional""
                            }
                        ]
                    }
                ]
            }";

            // Act
            var result = JsonConvert.DeserializeObject<DeltaResponseModel>(json);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual((decimal)-0.5f, result.ScoreChange);
            Assert.AreEqual((decimal)8.0f, result.OldScore);
            Assert.AreEqual((decimal)7.5f, result.NewScore);
            Assert.AreEqual(1, result.FunctionLevelFindings.Length);
            Assert.AreEqual("Calculate", result.FunctionLevelFindings[0].Function.Name);
        }

        #endregion

        #region PreFlightResponseModel Tests

        [TestMethod]
        public void PreFlightResponseModel_Deserialize_WithFileTypes()
        {
            // Arrange
            var json = @"{
                ""file-types"": ["".cs"", "".js"", "".py""]
            }";

            // Act
            var result = JsonConvert.DeserializeObject<PreFlightResponseModel>(json);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.FileTypes.Length);
            Assert.AreEqual(".cs", result.FileTypes[0]);
            Assert.AreEqual(".js", result.FileTypes[1]);
            Assert.AreEqual(".py", result.FileTypes[2]);
        }

        #endregion

        #region FnToRefactorModel Tests

        [TestMethod]
        public void FnToRefactorModel_Deserialize_WithAllFields()
        {
            // Arrange
            var json = @"{
                ""name"": ""ProcessOrder"",
                ""body"": ""function body here"",
                ""file-type"": ""cs"",
                ""nippy-b64"": ""base64data"",
                ""range"": {
                    ""start-line"": 10,
                    ""start-column"": 5,
                    ""end-line"": 50,
                    ""end-column"": 5
                },
                ""refactoring-targets"": [
                    {
                        ""name"": ""Extract Method""
                    }
                ]
            }";

            // Act
            var result = JsonConvert.DeserializeObject<FnToRefactorModel>(json);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("ProcessOrder", result.Name);
            Assert.AreEqual("function body here", result.Body);
            Assert.AreEqual("cs", result.FileType);
            Assert.AreEqual("base64data", result.NippyB64);
            Assert.AreEqual(10, result.Range.Startline);
            Assert.AreEqual(50, result.Range.EndLine);
        }

        [TestMethod]
        public void FnToRefactorModel_Serialize_ProducesValidJson()
        {
            // Arrange
            var model = new FnToRefactorModel
            {
                Name = "TestFunction",
                Body = "code",
                FileType = "js",
                Range = new CliRangeModel
                {
                    Startline = 1,
                    StartColumn = 1,
                    EndLine = 10,
                    EndColumn = 1
                }
            };

            // Act
            var json = JsonConvert.SerializeObject(model);
            var deserialized = JsonConvert.DeserializeObject<FnToRefactorModel>(json);

            // Assert
            Assert.AreEqual(model.Name, deserialized.Name);
            Assert.AreEqual(model.Body, deserialized.Body);
            Assert.AreEqual(model.FileType, deserialized.FileType);
            Assert.AreEqual(model.Range.Startline, deserialized.Range.Startline);
        }

        #endregion

        #region ReviewRequestModel Tests

        [TestMethod]
        public void ReviewRequestModel_Serialize_ProducesCorrectPropertyNames()
        {
            // Arrange
            var model = new ReviewRequestModel
            {
                FilePath = "test.cs",
                FileContent = "public class Test {}",
                CachePath = "/cache/path"
            };

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            Assert.IsTrue(json.Contains("\"path\""));
            Assert.IsTrue(json.Contains("\"file-content\""));
            Assert.IsTrue(json.Contains("\"cache-path\""));
        }

        [TestMethod]
        public void ReviewRequestModel_RoundTrip_PreservesData()
        {
            // Arrange
            var model = new ReviewRequestModel
            {
                FilePath = "test.cs",
                FileContent = "code content",
                CachePath = "/cache"
            };

            // Act
            var json = JsonConvert.SerializeObject(model);
            var deserialized = JsonConvert.DeserializeObject<ReviewRequestModel>(json);

            // Assert
            Assert.AreEqual(model.FilePath, deserialized.FilePath);
            Assert.AreEqual(model.FileContent, deserialized.FileContent);
            Assert.AreEqual(model.CachePath, deserialized.CachePath);
        }

        #endregion

        #region CliRangeModel Tests

        [TestMethod]
        public void CliRangeModel_Deserialize_WithKebabCaseProperties()
        {
            // Arrange
            var json = @"{
                ""start-line"": 10,
                ""start-column"": 5,
                ""end-line"": 20,
                ""end-column"": 15
            }";

            // Act
            var result = JsonConvert.DeserializeObject<CliRangeModel>(json);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(10, result.Startline);
            Assert.AreEqual(5, result.StartColumn);
            Assert.AreEqual(20, result.EndLine);
            Assert.AreEqual(15, result.EndColumn);
        }

        [TestMethod]
        public void CliRangeModel_Serialize_UsesKebabCaseProperties()
        {
            // Arrange
            var model = new CliRangeModel
            {
                Startline = 1,
                StartColumn = 1,
                EndLine = 100,
                EndColumn = 50
            };

            // Act
            var json = JsonConvert.SerializeObject(model);

            // Assert
            Assert.IsTrue(json.Contains("\"start-line\""));
            Assert.IsTrue(json.Contains("\"start-column\""));
            Assert.IsTrue(json.Contains("\"end-line\""));
            Assert.IsTrue(json.Contains("\"end-column\""));
        }

        #endregion

        #region CliCodeSmellModel Tests

        [TestMethod]
        public void CliCodeSmellModel_Deserialize_WithHighlightRange()
        {
            // Arrange
            var json = @"{
                ""category"": ""Deep Nesting"",
                ""details"": ""Depth: 5"",
                ""highlight-range"": {
                    ""start-line"": 15,
                    ""start-column"": 9,
                    ""end-line"": 25,
                    ""end-column"": 9
                }
            }";

            // Act
            var result = JsonConvert.DeserializeObject<CliCodeSmellModel>(json);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Deep Nesting", result.Category);
            Assert.AreEqual("Depth: 5", result.Details);
            Assert.IsNotNull(result.Range);
            Assert.AreEqual(15, result.Range.Startline);
            Assert.AreEqual(25, result.Range.EndLine);
        }

        #endregion

        #region FnsToRefactorCodeSmellRequestModel Tests

        [TestMethod]
        public void FnsToRefactorCodeSmellRequestModel_Serialize_IgnoresDefaultValues()
        {
            // Arrange
            var settings = new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore };
            var model = new FnsToRefactorCodeSmellRequestModel
            {
                FileName = "test.cs",
                FileContent = "code",
                CachePath = "/cache",
                CodeSmells = new List<CliCodeSmellModel>
                {
                    new CliCodeSmellModel { Category = "Test" }
                },
                Preflight = null // Should be ignored when null
            };

            // Act
            var json = JsonConvert.SerializeObject(model, settings);

            // Assert
            Assert.IsFalse(json.Contains("\"preflight\""), "Null preflight should not appear in JSON");
            Assert.IsTrue(json.Contains("\"file-name\""));
            Assert.IsTrue(json.Contains("\"file-content\""));
            Assert.IsTrue(json.Contains("\"code-smells\""));
        }

        #endregion
    }
}
