// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using System.Linq;
using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models.Agent;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Moq;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentOutputMapperTests
    {
        private Mock<ILogger> loggerMock;

        [TestInitialize]
        public void Setup()
        {
            loggerMock = new Mock<ILogger>();
        }

        [TestMethod]
        public void FixProposed_MapsConfidenceAndEmptyReasons()
        {
            var output = LoadFixture("fix-proposed.json");
            var fn = CreateTestFunction("if (a > 0) {\n    if (a > 1) {\n      return a;\n    }\n  }");
            var documentText = "if (a > 0) {\n    if (a > 1) {\n      return a;\n    }\n  }";

            var result = AgentOutputMapper.Map(output, documentText, fn, "test.ts", loggerMock.Object);

            var expected = (3, "Refactoring suggestion", "Review the suggested changes and apply if appropriate.", (string?)null);
            Assert.AreEqual(expected, Summarize(result.Confidence));
            Assert.AreEqual(0, result.Reasons.Length);
            Assert.AreEqual(("refactor-test-123", false), (result.TraceId, result.Metadata.Cached));
        }

        [TestMethod]
        public void UnableToFix_MapsTitleAndReason()
        {
            var output = LoadFixture("unable-to-fix.json");
            var fn = CreateTestFunction("test code");
            var documentText = "test code";

            var result = AgentOutputMapper.Map(output, documentText, fn, "test.ts", loggerMock.Object);

            var expected = (1, "Unable to refactor", "The agent could not find a suitable refactoring for this code.", (string?)"Review required");
            Assert.AreEqual(expected, Summarize(result.Confidence));
            CollectionAssert.AreEqual(new[] { "No changes proposed" }, ReasonSummaries(result));
            Assert.AreEqual(("test code", output.Reasoning), (result.Code, result.Confidence.RecommendedAction.Details));
        }

        [TestMethod]
        public void NeedsHumanReview_MapsReviewHeader()
        {
            var output = LoadFixture("needs-human-review.json");
            var fn = CreateTestFunction("try {\n    doSomething();\n  } catch (e) {\n    handleError(e);\n  }");
            var documentText = "try {\n    doSomething();\n  } catch (e) {\n    handleError(e);\n  }";

            var result = AgentOutputMapper.Map(output, documentText, fn, "test.ts", loggerMock.Object);

            var expected = (2, "Needs review", "The suggested changes require careful human review before applying.", (string?)"Review required");
            Assert.AreEqual(expected, Summarize(result.Confidence));
            CollectionAssert.AreEqual(new[] { "Refactoring results" }, ReasonSummaries(result));
        }

        [TestMethod]
        public void UnknownConfidence_IsLevel2()
        {
            var output = new AgentOutput
            {
                TaskId = "test-123",
                FixResult = "fix_proposed",
                Confidence = "unknown",
                Summary = "Test",
                Reasoning = "Test reasoning",
                Changes = new[]
                {
                    new AgentChange
                    {
                        File = "test.ts",
                        ChangeType = "partial",
                        Replacements = new[]
                        {
                            new AgentReplacement { Search = "oldCode", Replace = "newCode" },
                        },
                    },
                },
            };
            var fn = CreateTestFunction("test oldCode");
            var documentText = "test oldCode";

            var result = AgentOutputMapper.Map(output, documentText, fn, "test.ts", loggerMock.Object);

            Assert.AreEqual("test newCode", result.Code);
            Assert.AreEqual(2, result.Confidence.Level);
        }

        [TestMethod]
        public void NoOp_BecomesUnableToFixLow()
        {
            var output = new AgentOutput
            {
                TaskId = "test-123",
                FixResult = "fix_proposed",
                Confidence = "high",
                Summary = "Test",
                Reasoning = "Test reasoning",
                Changes = new AgentChange[0],
            };
            var fn = CreateTestFunction("original code");
            var documentText = "original code";

            var result = AgentOutputMapper.Map(output, documentText, fn, "test.ts", loggerMock.Object);

            Assert.AreEqual((1, "Unable to refactor"), (result.Confidence.Level, result.Confidence.Title));
            CollectionAssert.AreEqual(new[] { "No changes proposed" }, ReasonSummaries(result));
            Assert.AreEqual(("original code", "Test reasoning"), (result.Code, result.Confidence.RecommendedAction.Details));
        }

        [TestMethod]
        public void OtherFileChange_Ignored()
        {
            var output = new AgentOutput
            {
                TaskId = "test-123",
                FixResult = "fix_proposed",
                Confidence = "high",
                Summary = "Test",
                Reasoning = "Test reasoning",
                Changes = new[]
                {
                    new AgentChange
                    {
                        File = "other-file.ts",
                        ChangeType = "partial",
                        Replacements = new[]
                        {
                            new AgentReplacement { Search = "original", Replace = "modified" },
                        },
                    },
                },
            };
            var fn = CreateTestFunction("original code");
            var documentText = "original code";

            var result = AgentOutputMapper.Map(output, documentText, fn, "test.ts", loggerMock.Object);

            Assert.AreEqual("original code", result.Code);
        }

        private static (int Level, string Title, string Description, string? ReviewHeader) Summarize(ConfidenceModel confidence)
        {
            return (confidence.Level, confidence.Title, confidence.RecommendedAction.Description, confidence.ReviewHeader);
        }

        private static string[] ReasonSummaries(RefactorResponseModel result)
        {
            return result.Reasons.Select(r => r.Summary).ToArray();
        }

        private AgentOutput LoadFixture(string fileName)
        {
            var fixturePath = Path.Combine("Fixtures", "AgentOutput", fileName);
            var json = File.ReadAllText(fixturePath);
            return JsonConvert.DeserializeObject<AgentOutput>(json) ?? throw new System.InvalidOperationException("Fixture " + fileName + " is empty");
        }

        private FnToRefactorModel CreateTestFunction(string body)
        {
            return new FnToRefactorModel
            {
                Name = "testFunction",
                Body = body,
                Range = new CliRangeModel
                {
                    StartLine = 1,
                    StartColumn = 1,
                    EndLine = 1,
                    EndColumn = body.Length + 1,
                },
                RefactoringTargets = new[]
                {
                    new RefactoringTargetModel { Category = "test-smell", Line = 1 },
                },
            };
        }
    }
}
