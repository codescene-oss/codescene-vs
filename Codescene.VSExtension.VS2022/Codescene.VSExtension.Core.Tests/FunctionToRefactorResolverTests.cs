// Copyright (c) CodeScene. All rights reserved.

using System.Linq;
using Codescene.VSExtension.Core.Application;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class FunctionToRefactorResolverTests
{
    [TestMethod]
    public void CodeSmellInFunction_UsesFunctionWholeLines()
    {
        var content = "line1\nline2\nline3\nline4\nline5";
        var smell = new CodeSmellModel
        {
            Category = "Complex Method",
            FunctionName = "MyFunction",
            Range = new CodeRangeModel(2, 2, 5, 10),
            FunctionRange = new CodeRangeModel(2, 4, 1, 10),
        };

        var result = FunctionToRefactorResolver.ForCodeSmell("C:/test/file.cs", content, smell);

        Assert.IsNotNull(result);
        Assert.AreEqual(("MyFunction", "line2\nline3\nline4", "cs", "cs", (2, 1, 4, 6)), Summary(result));
        CollectionAssert.AreEqual(new[] { "Complex Method@2" }, TargetsOf(result));
    }

    [TestMethod]
    public void FileLevelSmell_UsesWholeFile()
    {
        var content = "line1\nline2\nline3";
        var smell = new CodeSmellModel
        {
            Category = "Large File",
            Range = new CodeRangeModel(1, 3, 1, 5),
            FunctionRange = null,
        };

        var result = FunctionToRefactorResolver.ForCodeSmell("C:/test/MyFile.cs", content, smell);

        Assert.IsNotNull(result);
        Assert.AreEqual(("MyFile.cs", "line1\nline2\nline3", "cs", "cs", (1, 1, 3, 6)), Summary(result));
    }

    [TestMethod]
    public void Body_PreservesCrlf()
    {
        var content = "line1\r\nline2\r\nline3";
        var smell = new CodeSmellModel
        {
            Category = "Complex Method",
            FunctionName = "MyFunction",
            Range = new CodeRangeModel(1, 1, 1, 5),
            FunctionRange = new CodeRangeModel(1, 3, 1, 5),
        };

        var result = FunctionToRefactorResolver.ForCodeSmell("C:/test/file.cs", content, smell);

        Assert.AreEqual("line1\r\nline2\r\nline3", result.Body);
    }

    [TestMethod]
    public void EndLineBeyondFile_IsClamped()
    {
        var content = "line1\nline2\nline3";
        var smell = new CodeSmellModel
        {
            Category = "Complex Method",
            FunctionName = "MyFunction",
            Range = new CodeRangeModel(2, 2, 1, 5),
            FunctionRange = new CodeRangeModel(2, 100, 1, 5),
        };

        var result = FunctionToRefactorResolver.ForCodeSmell("C:/test/file.cs", content, smell);

        Assert.AreEqual(3, result.Range.EndLine);
        Assert.AreEqual("line2\nline3", result.Body);
    }

    [TestMethod]
    public void FileAndFunctionType_AreExtension()
    {
        var content = "console.log('test');";
        var smell = new CodeSmellModel
        {
            Category = "Complex Method",
            FunctionName = "myFunc",
            Range = new CodeRangeModel(1, 1, 1, 20),
            FunctionRange = new CodeRangeModel(1, 1, 1, 20),
        };

        var result = FunctionToRefactorResolver.ForCodeSmell("C:/project/module.js", content, smell);

        Assert.AreEqual("js", result.FileType);
        Assert.AreEqual("js", result.FunctionType);
    }

    [TestMethod]
    public void Finding_SkipsFixedAndLineless()
    {
        var content = "line1\nline2\nline3\nline4\nline5";
        var finding = new FunctionFindingModel
        {
            Function = new FunctionInfoModel
            {
                Name = "TestFunc",
                Range = new CliRangeModel
                {
                    StartLine = 2,
                    EndLine = 4,
                    StartColumn = 1,
                    EndColumn = 10,
                },
            },
            ChangeDetails = new[]
            {
                new ChangeDetailModel { Category = "Issue1", ChangeType = ChangeType.Fixed, Line = 2 },
                new ChangeDetailModel { Category = "Issue2", ChangeType = ChangeType.Degraded, Line = 3 },
                new ChangeDetailModel { Category = "Issue3", ChangeType = ChangeType.Introduced, Line = null },
                new ChangeDetailModel { Category = "Issue4", ChangeType = ChangeType.Improved, Line = 4 },
            },
        };

        var result = FunctionToRefactorResolver.ForFinding("C:/test/file.cs", content, finding);

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(new[] { "Issue2@3", "Issue4@4" }, TargetsOf(result));
    }

    [TestMethod]
    public void Finding_NoTargets_ReturnsNull()
    {
        var content = "line1\nline2\nline3";
        var finding = new FunctionFindingModel
        {
            Function = new FunctionInfoModel
            {
                Name = "TestFunc",
                Range = new CliRangeModel { StartLine = 1, EndLine = 3, StartColumn = 1, EndColumn = 5 },
            },
            ChangeDetails = new[]
            {
                new ChangeDetailModel { Category = "Issue1", ChangeType = ChangeType.Fixed, Line = 2 },
            },
        };

        var result = FunctionToRefactorResolver.ForFinding("C:/test/file.cs", content, finding);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Finding_NoRange_ReturnsNull()
    {
        var content = "line1\nline2\nline3";
        var finding = new FunctionFindingModel
        {
            Function = new FunctionInfoModel
            {
                Name = "TestFunc",
                Range = null,
            },
            ChangeDetails = new[]
            {
                new ChangeDetailModel { Category = "Issue1", ChangeType = ChangeType.Degraded, Line = 2 },
            },
        };

        var result = FunctionToRefactorResolver.ForFinding("C:/test/file.cs", content, finding);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void WholeFile_KeepsGivenTargets()
    {
        var content = "line1\nline2\nline3";
        var targets = new[]
        {
            new Codescene.VSExtension.Core.Models.Cli.Refactor.RefactoringTargetModel
            {
                Category = "Complex Method",
                Line = 1,
            },
            new Codescene.VSExtension.Core.Models.Cli.Refactor.RefactoringTargetModel
            {
                Category = "Brain Method",
                Line = 2,
            },
        };

        var result = FunctionToRefactorResolver.ForWholeFile("C:/test/MyFile.py", content, targets);

        Assert.IsNotNull(result);
        Assert.AreEqual(("MyFile.py", "py", "py"), (result.Name, result.FileType, result.FunctionType));
        CollectionAssert.AreEqual(new[] { "Complex Method@1", "Brain Method@2" }, TargetsOf(result));
    }

    [TestMethod]
    public void ForCodeSmell_TargetIsHighlightStartLine()
    {
        var content = "line1\nline2\nline3\nline4\nline5";
        var smell = new CodeSmellModel
        {
            Category = "Complex Conditional",
            FunctionName = "Calculate",
            Range = new CodeRangeModel(3, 4, 5, 10),
            FunctionRange = new CodeRangeModel(2, 5, 1, 5),
        };

        var result = FunctionToRefactorResolver.ForCodeSmell("C:/test/file.cs", content, smell);

        Assert.IsNotNull(result);
        CollectionAssert.AreEqual(new[] { "Complex Conditional@3" }, TargetsOf(result));
    }

    [TestMethod]
    public void WholeFile_EmptyContentWithoutExtension_UsesSingleEmptyLine()
    {
        var result = FunctionToRefactorResolver.ForWholeFile("C:/test/Makefile", string.Empty, new RefactoringTargetModel[0]);

        Assert.AreEqual(("Makefile", string.Empty, string.Empty, string.Empty, (1, 1, 1, 1)), Summary(result));
    }

    private static (string Name, string Body, string FileType, string FunctionType, (int, int, int, int) Range) Summary(FnToRefactorModel result)
    {
        var range = result.Range;
        return (result.Name, result.Body, result.FileType, result.FunctionType, (range.StartLine, range.StartColumn, range.EndLine, range.EndColumn));
    }

    private static string[] TargetsOf(FnToRefactorModel result)
    {
        return result.RefactoringTargets.Select(t => $"{t.Category}@{t.Line}").ToArray();
    }
}
