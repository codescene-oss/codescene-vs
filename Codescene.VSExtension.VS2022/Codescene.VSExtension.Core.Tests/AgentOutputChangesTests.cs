// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Agent;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Agent;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AgentOutputChangesTests
    {
        private Mock<ILogger> loggerMock;

        [TestInitialize]
        public void Setup()
        {
            loggerMock = new Mock<ILogger>();
        }

        [TestMethod]
        public void Partial_ExactMatch()
        {
            var output = new AgentOutput
            {
                TaskId = "test-123",
                FixResult = "fix_proposed",
                Confidence = "high",
                Summary = "Test",
                Reasoning = "Test",
                Changes = new[]
                {
                    new AgentChange
                    {
                        File = "test.ts",
                        ChangeType = "partial",
                        Replacements = new[]
                        {
                            new AgentReplacement
                            {
                                Search = "old code",
                                Replace = "new code",
                            },
                        },
                    },
                },
            };
            var fn = CreateTestFunction("old code");
            var documentText = "old code";

            var result = AgentOutputMapper.Map(output, documentText, fn, "test.ts", loggerMock.Object);

            Assert.AreEqual("new code", result.Code);
        }

        [TestMethod]
        public void Partial_TrailingNewlineTrimmedMatch()
        {
            var output = new AgentOutput
            {
                TaskId = "test-123",
                FixResult = "fix_proposed",
                Confidence = "high",
                Summary = "Test",
                Reasoning = "Test",
                Changes = new[]
                {
                    new AgentChange
                    {
                        File = "test.ts",
                        ChangeType = "partial",
                        Replacements = new[]
                        {
                            new AgentReplacement
                            {
                                Search = "old code\n\n",
                                Replace = "new code",
                            },
                        },
                    },
                },
            };
            var fn = CreateTestFunction("old code");
            var documentText = "old code";

            var result = AgentOutputMapper.Map(output, documentText, fn, "test.ts", loggerMock.Object);

            Assert.AreEqual("new code", result.Code);
        }

        [TestMethod]
        public void Partial_Unmatched_SkippedWithWarning()
        {
            var output = new AgentOutput
            {
                TaskId = "test-123",
                FixResult = "fix_proposed",
                Confidence = "high",
                Summary = "Test",
                Reasoning = "Test",
                Changes = new[]
                {
                    new AgentChange
                    {
                        File = "test.ts",
                        ChangeType = "partial",
                        Replacements = new[]
                        {
                            new AgentReplacement
                            {
                                Search = "nonexistent code",
                                Replace = "new code",
                            },
                        },
                    },
                },
            };
            var fn = CreateTestFunction("old code");
            var documentText = "old code";

            var result = AgentOutputMapper.Map(output, documentText, fn, "test.ts", loggerMock.Object);

            Assert.AreEqual("old code", result.Code);
            loggerMock.Verify(x => x.Warn(It.Is<string>(s => s.Contains("not found")), It.IsAny<bool>()), Times.Once);
        }

        [TestMethod]
        public void WholeFile_UnchangedSurroundings_Accepted()
        {
            var documentText = "prefix\nfunction body\nsuffix";
            var output = new AgentOutput
            {
                TaskId = "test-123",
                FixResult = "fix_proposed",
                Confidence = "high",
                Summary = "Test",
                Reasoning = "Test",
                Changes = new[]
                {
                    new AgentChange
                    {
                        File = "test.ts",
                        ChangeType = "whole_file",
                        WholeFileContent = "prefix\nnew function body\nsuffix",
                    },
                },
            };
            var fn = new FnToRefactorModel
            {
                Name = "test",
                Body = "function body",
                Range = new CliRangeModel
                {
                    StartLine = 2,
                    StartColumn = 1,
                    EndLine = 2,
                    EndColumn = 14,
                },
                RefactoringTargets = new[]
                {
                    new RefactoringTargetModel { Category = "test", Line = 2 },
                },
            };

            var result = AgentOutputMapper.Map(output, documentText, fn, "test.ts", loggerMock.Object);

            Assert.AreEqual("new function body", result.Code);
        }

        [TestMethod]
        public void WholeFile_ChangedOutside_IgnoredWithWarning()
        {
            var documentText = "prefix\nfunction body\nsuffix";
            var output = new AgentOutput
            {
                TaskId = "test-123",
                FixResult = "fix_proposed",
                Confidence = "high",
                Summary = "Test",
                Reasoning = "Test",
                Changes = new[]
                {
                    new AgentChange
                    {
                        File = "test.ts",
                        ChangeType = "whole_file",
                        WholeFileContent = "changed prefix\nfunction body\nsuffix",
                    },
                },
            };
            var fn = new FnToRefactorModel
            {
                Name = "test",
                Body = "function body",
                Range = new CliRangeModel
                {
                    StartLine = 2,
                    StartColumn = 1,
                    EndLine = 2,
                    EndColumn = 14,
                },
                RefactoringTargets = new[]
                {
                    new RefactoringTargetModel { Category = "test", Line = 2 },
                },
            };

            var result = AgentOutputMapper.Map(output, documentText, fn, "test.ts", loggerMock.Object);

            Assert.AreEqual("function body", result.Code);
            loggerMock.Verify(x => x.Warn(It.Is<string>(s => s.Contains("outside function range")), It.IsAny<bool>()), Times.Once);
        }

        [TestMethod]
        public void Crlf_Restored()
        {
            var output = new AgentOutput
            {
                TaskId = "test-123",
                FixResult = "fix_proposed",
                Confidence = "high",
                Summary = "Test",
                Reasoning = "Test",
                Changes = new[]
                {
                    new AgentChange
                    {
                        File = "test.ts",
                        ChangeType = "partial",
                        Replacements = new[]
                        {
                            new AgentReplacement
                            {
                                Search = "line1\nline2",
                                Replace = "newline1\nnewline2",
                            },
                        },
                    },
                },
            };
            var fn = CreateTestFunction("line1\r\nline2");
            var documentText = "line1\r\nline2";

            var result = AgentOutputMapper.Map(output, documentText, fn, "test.ts", loggerMock.Object);

            Assert.IsTrue(result.Code.Contains("\r\n"));
            Assert.AreEqual("newline1\r\nnewline2", result.Code);
        }

        [TestMethod]
        public void Partial_WithoutReplacements_KeepsBody()
        {
            var result = MapSingleChange(new AgentChange { File = "test.ts", ChangeType = "partial" }, "old code", CreateTestFunction("old code"));

            Assert.AreEqual("old code", result.Code);
        }

        [TestMethod]
        public void WholeFile_RangeOutsideDocument_IgnoredWithWarning()
        {
            var fn = CreateTestFunction("body");
            fn.Range.StartLine = 5;
            fn.Range.EndLine = 5;

            var result = MapSingleChange(WholeFileChange("prefix\nnew body"), "body", fn);

            Assert.AreEqual("body", result.Code);
            loggerMock.Verify(l => l.Warn("Agent whole-file change ignored: invalid function range", It.IsAny<bool>()));
        }

        [TestMethod]
        public void WholeFile_ContentShorterThanSurroundings_IgnoredWithWarning()
        {
            var fn = CreateTestFunction("body");
            fn.Range.StartLine = 2;
            fn.Range.EndLine = 2;

            var result = MapSingleChange(WholeFileChange("prefix"), "prefix\nbody\nsuffix", fn);

            Assert.AreEqual("body", result.Code);
            loggerMock.Verify(l => l.Warn("Agent whole-file change ignored: content outside function range was modified", It.IsAny<bool>()));
        }

        [TestMethod]
        public void UnknownFixResult_UsesSummaryAndRequiresReview()
        {
            var change = new AgentChange
            {
                File = "test.ts",
                ChangeType = "partial",
                Replacements = new[] { new AgentReplacement { Search = "old", Replace = "new" } },
            };

            var result = MapSingleChange(change, "old", CreateTestFunction("old"), "something_new");

            Assert.AreEqual(("new", "Test", "Review required"), (result.Code, result.Confidence.Title, result.Confidence.ReviewHeader));
        }

        private static AgentChange WholeFileChange(string content)
        {
            return new AgentChange { File = "test.ts", ChangeType = "whole_file", WholeFileContent = content };
        }

        private RefactorResponseModel MapSingleChange(AgentChange change, string documentText, FnToRefactorModel fn, string fixResult = "fix_proposed")
        {
            var output = new AgentOutput
            {
                TaskId = "test-123",
                FixResult = fixResult,
                Confidence = "high",
                Summary = "Test",
                Reasoning = "Test",
                Changes = new[] { change },
            };

            return AgentOutputMapper.Map(output, documentText, fn, "test.ts", loggerMock.Object);
        }

        private FnToRefactorModel CreateTestFunction(string body)
        {
            return new FnToRefactorModel
            {
                Name = "test",
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
                    new RefactoringTargetModel { Category = "test", Line = 1 },
                },
            };
        }
    }
}
