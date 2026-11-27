// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class RefactorableFunctionEnricherTests
    {
        private RefactorableFunctionEnricher _enricher;

        [TestInitialize]
        public void Setup()
        {
            _enricher = new RefactorableFunctionEnricher();
        }

        [TestMethod]
        public void SetsRefactorableFnForFindingsWithTargets()
        {
            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "TestFunction",
                            Range = new CliRangeModel
                            {
                                StartLine = 10,
                                EndLine = 20,
                                StartColumn = 1,
                                EndColumn = 2,
                            },
                        },
                        ChangeDetails = new[]
                        {
                            new ChangeDetailModel
                            {
                                Category = "Complex Method",
                                ChangeType = ChangeType.Improved,
                                Line = 15,
                            },
                        },
                    },
                },
            };

            var content = "line1\nline2\nline3\nline4\nline5\nline6\nline7\nline8\nline9\nline10\nline11\nline12\nline13\nline14\nline15\nline16\nline17\nline18\nline19\nline20\n";

            _enricher.Enrich(delta, "test.cs", content);

            Assert.IsNotNull(delta.FunctionLevelFindings[0].RefactorableFn);
            Assert.AreEqual("TestFunction", delta.FunctionLevelFindings[0].RefactorableFn.Name);
            Assert.AreEqual("cs", delta.FunctionLevelFindings[0].RefactorableFn.FileType);
        }

        [TestMethod]
        public void LeavesFindingsWithoutTargetsNull()
        {
            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "TestFunction",
                            Range = new CliRangeModel
                            {
                                StartLine = 10,
                                EndLine = 20,
                                StartColumn = 1,
                                EndColumn = 2,
                            },
                        },
                        ChangeDetails = null,
                    },
                },
            };

            var content = "line1\nline2\n";

            _enricher.Enrich(delta, "test.cs", content);

            Assert.IsNull(delta.FunctionLevelFindings[0].RefactorableFn);
        }

        [TestMethod]
        public void NullDelta_NoOp()
        {
            _enricher.Enrich(null, "test.cs", "content");
        }

        [TestMethod]
        public void AlwaysEnriches_RegardlessOfEnabledState()
        {
            var delta = new DeltaResponseModel
            {
                FunctionLevelFindings = new[]
                {
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "Function1",
                            Range = new CliRangeModel
                            {
                                StartLine = 1,
                                EndLine = 5,
                                StartColumn = 1,
                                EndColumn = 1,
                            },
                        },
                        ChangeDetails = new[]
                        {
                            new ChangeDetailModel
                            {
                                Category = "Complex Method",
                                ChangeType = ChangeType.Improved,
                                Line = 3,
                            },
                        },
                    },
                    new FunctionFindingModel
                    {
                        Function = new FunctionInfoModel
                        {
                            Name = "Function2",
                            Range = new CliRangeModel
                            {
                                StartLine = 10,
                                EndLine = 15,
                                StartColumn = 1,
                                EndColumn = 1,
                            },
                        },
                        ChangeDetails = new[]
                        {
                            new ChangeDetailModel
                            {
                                Category = "Deep Nesting",
                                ChangeType = ChangeType.Degraded,
                                Line = 12,
                            },
                        },
                    },
                },
            };

            var content = "line1\nline2\nline3\nline4\nline5\nline6\nline7\nline8\nline9\nline10\nline11\nline12\nline13\nline14\nline15\n";

            _enricher.Enrich(delta, "test.js", content);

            Assert.IsNotNull(delta.FunctionLevelFindings[0].RefactorableFn);
            Assert.AreEqual("Function1", delta.FunctionLevelFindings[0].RefactorableFn.Name);
            Assert.IsNotNull(delta.FunctionLevelFindings[1].RefactorableFn);
            Assert.AreEqual("Function2", delta.FunctionLevelFindings[1].RefactorableFn.Name);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        public void NoContent_LeavesFindingsUntouched(string? content)
        {
            var finding = new FunctionFindingModel
            {
                Function = new FunctionInfoModel
                {
                    Name = "TestFunction",
                    Range = new CliRangeModel { StartLine = 1, EndLine = 2, StartColumn = 1, EndColumn = 2 },
                },
                ChangeDetails = new[]
                {
                    new ChangeDetailModel { Category = "Complex Method", ChangeType = ChangeType.Improved, Line = 1 },
                },
            };

            _enricher.Enrich(new DeltaResponseModel { FunctionLevelFindings = new[] { finding } }, "test.cs", content);

            Assert.IsNull(finding.RefactorableFn);
        }
    }
}
