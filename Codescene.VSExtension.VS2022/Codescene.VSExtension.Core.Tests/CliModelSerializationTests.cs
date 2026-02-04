// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Telemetry;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliModelSerializationTests
    {
        [TestMethod]
        public void PositionModel_Deserialize_WithAllFields()
        {
            var json = @"{ ""line"": 10, ""character"": 5 }";

            var result = DeserializeJson<PositionModel>(json);

            Assert.AreEqual(10, result.Line);
            Assert.AreEqual(5, result.Character);
        }

        [TestMethod]
        public void PositionModel_Serialize_UsesCorrectPropertyNames()
        {
            var model = new PositionModel { Line = 42, Character = 15 };

            var json = JsonConvert.SerializeObject(model);

            AssertJsonContainsProperties(json, "line", "character");
        }

        [TestMethod]
        public void ConfidenceModel_Deserialize_WithAllFields()
        {
            var json = @"{
                ""level"": 3,
                ""recommended-action"": { ""description"": ""Review carefully"", ""details"": ""Check edge cases"" },
                ""review-header"": ""Medium Confidence"",
                ""title"": ""Refactoring Suggestion""
            }";

            var result = DeserializeJson<ConfidenceModel>(json);

            Assert.AreEqual(3, result.Level);
            Assert.AreEqual("Review carefully", result.RecommendedAction.Description);
            Assert.AreEqual("Check edge cases", result.RecommendedAction.Details);
            Assert.AreEqual("Medium Confidence", result.ReviewHeader);
            Assert.AreEqual("Refactoring Suggestion", result.Title);
        }

        [TestMethod]
        public void ConfidenceModel_Serialize_UsesKebabCasePropertyNames()
        {
            var model = new ConfidenceModel
            {
                Level = 2,
                RecommendedAction = new RecommendedActionModel { Description = "Test", Details = "Details" },
                ReviewHeader = "Header",
                Title = "Title",
            };

            var json = JsonConvert.SerializeObject(model);

            AssertJsonContainsProperties(json, "level", "recommended-action", "review-header", "title");
        }

        [TestMethod]
        public void CreditsInfoModel_Deserialize_WithAllFields()
        {
            var json = @"{ ""limit"": 100, ""reset"": ""2024-01-15T00:00:00Z"", ""used"": 25 }";

            var result = DeserializeJson<CreditsInfoModel>(json);

            Assert.AreEqual(100, result.Limit);
            Assert.AreEqual("2024-01-15T00:00:00Z", result.Reset);
            Assert.AreEqual(25, result.Used);
        }

        [TestMethod]
        public void CreditsInfoModel_Serialize_UsesCorrectPropertyNames()
        {
            var model = new CreditsInfoModel { Limit = 50, Reset = "2024-02-01T00:00:00Z", Used = 10 };

            var json = JsonConvert.SerializeObject(model);

            AssertJsonContainsProperties(json, "limit", "reset", "used");
        }

        [TestMethod]
        public void CreditsInfoErrorModel_Deserialize_WithAllFields()
        {
            var json = @"{
                ""credits-info"": { ""limit"": 100, ""reset"": ""2024-01-15T00:00:00Z"", ""used"": 100 },
                ""message"": ""Credit limit exceeded"",
                ""trace-id"": ""abc-123-def""
            }";

            var result = DeserializeJson<CreditsInfoErrorModel>(json);

            Assert.AreEqual(100, result.CreditsInfo.Limit);
            Assert.AreEqual(100, result.CreditsInfo.Used);
            Assert.AreEqual("Credit limit exceeded", result.Message);
            Assert.AreEqual("abc-123-def", result.TraceId);
        }

        [TestMethod]
        public void CreditsInfoErrorModel_Serialize_UsesKebabCasePropertyNames()
        {
            var model = new CreditsInfoErrorModel
            {
                CreditsInfo = new CreditsInfoModel { Limit = 50, Used = 50 },
                Message = "Error",
                TraceId = "trace-123",
            };

            var json = JsonConvert.SerializeObject(model);

            AssertJsonContainsProperties(json, "credits-info", "message", "trace-id");
        }

        [TestMethod]
        public void MetadataModel_Deserialize_WithCachedTrue()
        {
            var json = @"{ ""cached?"": true }";

            var result = DeserializeJson<MetadataModel>(json);

            Assert.IsTrue(result.Cached);
        }

        [TestMethod]
        public void MetadataModel_Deserialize_WithCachedFalse()
        {
            var json = @"{ ""cached?"": false }";

            var result = DeserializeJson<MetadataModel>(json);

            Assert.IsFalse(result.Cached);
        }

        [TestMethod]
        public void MetadataModel_Serialize_UsesQuestionMarkPropertyName()
        {
            var model = new MetadataModel { Cached = true };

            var json = JsonConvert.SerializeObject(model);

            Assert.Contains("\"cached?\"", json);
        }

        [TestMethod]
        public void ReasonDetailsModel_Deserialize_WithAllFields()
        {
            var json = @"{
                ""message"": ""Deeply nested code detected"",
                ""lines"": [10, 25],
                ""columns"": [4, 50]
            }";

            var result = DeserializeJson<ReasonDetailsModel>(json);

            Assert.AreEqual("Deeply nested code detected", result.Message);
            CollectionAssert.AreEqual(new[] { 10, 25 }, result.Lines);
            CollectionAssert.AreEqual(new[] { 4, 50 }, result.Columns);
        }

        [TestMethod]
        public void ReasonDetailsModel_Serialize_UsesCorrectPropertyNames()
        {
            var model = new ReasonDetailsModel
            {
                Message = "Test message",
                Lines = new[] { 1, 10 },
                Columns = new[] { 0, 80 },
            };

            var json = JsonConvert.SerializeObject(model);

            AssertJsonContainsProperties(json, "message", "lines", "columns");
        }

        [TestMethod]
        public void ReasonModel_Deserialize_WithAllFields()
        {
            var json = @"{
                ""summary"": ""Code complexity issues"",
                ""details"": [
                    { ""message"": ""Issue 1"", ""lines"": [5, 10], ""columns"": [0, 40] },
                    { ""message"": ""Issue 2"", ""lines"": [15, 20], ""columns"": [0, 60] }
                ]
            }";

            var result = DeserializeJson<ReasonModel>(json);

            Assert.AreEqual("Code complexity issues", result.Summary);
            Assert.HasCount(2, result.Details);
            Assert.AreEqual("Issue 1", result.Details[0].Message);
            Assert.AreEqual("Issue 2", result.Details[1].Message);
        }

        [TestMethod]
        public void ReasonModel_Serialize_UsesCorrectPropertyNames()
        {
            var model = new ReasonModel
            {
                Summary = "Summary text",
                Details = new[] { new ReasonDetailsModel { Message = "Detail" } },
            };

            var json = JsonConvert.SerializeObject(model);

            AssertJsonContainsProperties(json, "summary", "details");
        }

        [TestMethod]
        public void RecommendedActionModel_Deserialize_WithAllFields()
        {
            var json = @"{ ""description"": ""Review the refactoring"", ""details"": ""Check for edge cases"" }";

            var result = DeserializeJson<RecommendedActionModel>(json);

            Assert.AreEqual("Review the refactoring", result.Description);
            Assert.AreEqual("Check for edge cases", result.Details);
        }

        [TestMethod]
        public void RecommendedActionModel_Serialize_UsesCorrectPropertyNames()
        {
            var model = new RecommendedActionModel { Description = "Desc", Details = "Det" };

            var json = JsonConvert.SerializeObject(model);

            AssertJsonContainsProperties(json, "description", "details");
        }

        [TestMethod]
        public void RefactoringPropertiesModel_Deserialize_WithAllFields()
        {
            var json = @"{
                ""added-code-smells"": [""Complex Conditional"", ""Deep Nesting""],
                ""removed-code-smells"": [""Long Method""]
            }";

            var result = DeserializeJson<RefactoringPropertiesModel>(json);

            CollectionAssert.AreEqual(new[] { "Complex Conditional", "Deep Nesting" }, result.AddedCodeSmells);
            CollectionAssert.AreEqual(new[] { "Long Method" }, result.RemovedCodeSmells);
        }

        [TestMethod]
        public void RefactoringPropertiesModel_Serialize_UsesKebabCasePropertyNames()
        {
            var model = new RefactoringPropertiesModel
            {
                AddedCodeSmells = new[] { "Smell1" },
                RemovedCodeSmells = new[] { "Smell2" },
            };

            var json = JsonConvert.SerializeObject(model);

            AssertJsonContainsProperties(json, "added-code-smells", "removed-code-smells");
        }

        [TestMethod]
        public void CliCodeHealthRulesErrorModel_Deserialize_WithAllFields()
        {
            var json = @"{ ""description"": ""Invalid rule configuration"", ""remedy"": ""Check the rule syntax"" }";

            var result = DeserializeJson<CliCodeHealthRulesErrorModel>(json);

            Assert.AreEqual("Invalid rule configuration", result.Description);
            Assert.AreEqual("Check the rule syntax", result.Remedy);
        }

        [TestMethod]
        public void CliCodeHealthRulesErrorModel_Serialize_UsesCorrectPropertyNames()
        {
            var model = new CliCodeHealthRulesErrorModel { Description = "Error", Remedy = "Fix it" };

            var json = JsonConvert.SerializeObject(model);

            AssertJsonContainsProperties(json, "description", "remedy");
        }

        [TestMethod]
        public void TelemetryEvent_WithEventName_SetsEventNameAndReturnsSelf()
        {
            var telemetryEvent = new TelemetryEvent();

            var result = telemetryEvent.WithEventName("test-event");

            Assert.AreSame(telemetryEvent, result);
            Assert.AreEqual("test-event", telemetryEvent.EventName);
        }

        [TestMethod]
        public void TelemetryEvent_WithUserId_SetsUserIdAndReturnsSelf()
        {
            var telemetryEvent = new TelemetryEvent();

            var result = telemetryEvent.WithUserId("user-123");

            Assert.AreSame(telemetryEvent, result);
            Assert.AreEqual("user-123", telemetryEvent.UserId);
        }

        [TestMethod]
        public void TelemetryEvent_WithEditorType_SetsEditorTypeAndReturnsSelf()
        {
            var telemetryEvent = new TelemetryEvent();

            var result = telemetryEvent.WithEditorType("vs2022");

            Assert.AreSame(telemetryEvent, result);
            Assert.AreEqual("vs2022", telemetryEvent.EditorType);
        }

        [TestMethod]
        public void TelemetryEvent_WithExtensionVersion_SetsExtensionVersionAndReturnsSelf()
        {
            var telemetryEvent = new TelemetryEvent();

            var result = telemetryEvent.WithExtensionVersion("1.2.3");

            Assert.AreSame(telemetryEvent, result);
            Assert.AreEqual("1.2.3", telemetryEvent.ExtensionVersion);
        }

        [TestMethod]
        public void TelemetryEvent_WithInternal_SetsInternalAndReturnsSelf()
        {
            var telemetryEvent = new TelemetryEvent();

            var result = telemetryEvent.WithInternal(true);

            Assert.AreSame(telemetryEvent, result);
            Assert.IsTrue(telemetryEvent.Internal);
        }

        [TestMethod]
        public void TelemetryEvent_FluentChaining_SetsAllProperties()
        {
            var telemetryEvent = new TelemetryEvent()
                .WithEventName("code-review")
                .WithUserId("user-456")
                .WithEditorType("vs2022")
                .WithExtensionVersion("2.0.0")
                .WithInternal(false);

            Assert.AreEqual("code-review", telemetryEvent.EventName);
            Assert.AreEqual("user-456", telemetryEvent.UserId);
            Assert.AreEqual("vs2022", telemetryEvent.EditorType);
            Assert.AreEqual("2.0.0", telemetryEvent.ExtensionVersion);
            Assert.IsFalse(telemetryEvent.Internal);
        }

        [TestMethod]
        public void TelemetryEvent_Serialize_UsesKebabCasePropertyNames()
        {
            var telemetryEvent = new TelemetryEvent
            {
                EventName = "test",
                UserId = "user",
                EditorType = "editor",
                ExtensionVersion = "1.0",
                Internal = true,
            };

            var json = JsonConvert.SerializeObject(telemetryEvent);

            AssertJsonContainsProperties(json, "event-name", "user-id", "editor-type", "extension-version", "internal");
        }

        [TestMethod]
        public void TelemetryEvent_Serialize_OmitsNullValues()
        {
            var telemetryEvent = new TelemetryEvent { EventName = "test" };

            var json = JsonConvert.SerializeObject(telemetryEvent);

            Assert.Contains("\"event-name\"", json);
            Assert.DoesNotContain("\"user-id\"", json);
            Assert.DoesNotContain("\"editor-type\"", json);
            Assert.DoesNotContain("\"extension-version\"", json);
            Assert.DoesNotContain("\"internal\"", json);
        }

        [TestMethod]
        public void TelemetryEvent_Deserialize_WithAllFields()
        {
            var json = @"{
                ""event-name"": ""refactor"",
                ""user-id"": ""abc123"",
                ""editor-type"": ""vs2022"",
                ""extension-version"": ""3.0.0"",
                ""internal"": true
            }";

            var result = DeserializeJson<TelemetryEvent>(json);

            Assert.AreEqual("refactor", result.EventName);
            Assert.AreEqual("abc123", result.UserId);
            Assert.AreEqual("vs2022", result.EditorType);
            Assert.AreEqual("3.0.0", result.ExtensionVersion);
            Assert.IsTrue(result.Internal);
        }

        private static T DeserializeJson<T>(string json)
        {
            var result = JsonConvert.DeserializeObject<T>(json);
            Assert.IsNotNull(result, $"Deserialization of {typeof(T).Name} returned null");
            return result;
        }

        private static void AssertJsonContainsProperties(string json, params string[] properties)
        {
            foreach (var prop in properties)
            {
                Assert.Contains($"\"{prop}\"", json, $"JSON should contain property '{prop}'");
            }
        }
    }
}
