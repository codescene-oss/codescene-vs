// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application;
using Codescene.VSExtension.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class CodeSmellLookupTests
{
    [TestMethod]
    public void ReturnsFirstOverlapping()
    {
        var smells = new[]
        {
            new CodeSmellModel
            {
                Category = "Smell1",
                Range = new CodeRangeModel(1, 2, 1, 10),
            },
            new CodeSmellModel
            {
                Category = "Smell2",
                Range = new CodeRangeModel(5, 7, 1, 10),
            },
            new CodeSmellModel
            {
                Category = "Smell3",
                Range = new CodeRangeModel(10, 12, 1, 10),
            },
        };

        var result = CodeSmellLookup.FirstOverlapping(smells, 6, 8);

        Assert.IsNotNull(result);
        Assert.AreEqual("Smell2", result.Category);
    }

    [TestMethod]
    public void NoneOverlapping_ReturnsNull()
    {
        var smells = new[]
        {
            new CodeSmellModel
            {
                Category = "Smell1",
                Range = new CodeRangeModel(1, 2, 1, 10),
            },
            new CodeSmellModel
            {
                Category = "Smell2",
                Range = new CodeRangeModel(10, 12, 1, 10),
            },
        };

        var result = CodeSmellLookup.FirstOverlapping(smells, 5, 8);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void FileLevelSmell_Considered()
    {
        var smells = new[]
        {
            new CodeSmellModel
            {
                Category = "Large File",
                Range = new CodeRangeModel(1, 100, 1, 10),
                FunctionRange = null,
            },
        };

        var result = CodeSmellLookup.FirstOverlapping(smells, 50, 50);

        Assert.IsNotNull(result);
        Assert.AreEqual("Large File", result.Category);
    }

    [TestMethod]
    public void NullSmells_ReturnsNull()
    {
        var result = CodeSmellLookup.FirstOverlapping(null, 1, 10);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void EmptySmells_ReturnsNull()
    {
        var result = CodeSmellLookup.FirstOverlapping(new CodeSmellModel[0], 1, 10);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void RangeTouchesSmellStart_Overlaps()
    {
        var smells = new[]
        {
            new CodeSmellModel
            {
                Category = "Smell1",
                Range = new CodeRangeModel(5, 10, 1, 10),
            },
        };

        var result = CodeSmellLookup.FirstOverlapping(smells, 1, 5);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void RangeTouchesSmellEnd_Overlaps()
    {
        var smells = new[]
        {
            new CodeSmellModel
            {
                Category = "Smell1",
                Range = new CodeRangeModel(5, 10, 1, 10),
            },
        };

        var result = CodeSmellLookup.FirstOverlapping(smells, 10, 15);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void RangeContainsSmell_Overlaps()
    {
        var smells = new[]
        {
            new CodeSmellModel
            {
                Category = "Smell1",
                Range = new CodeRangeModel(5, 7, 1, 10),
            },
        };

        var result = CodeSmellLookup.FirstOverlapping(smells, 3, 10);

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void SmellContainsRange_Overlaps()
    {
        var smells = new[]
        {
            new CodeSmellModel
            {
                Category = "Smell1",
                Range = new CodeRangeModel(5, 15, 1, 10),
            },
        };

        var result = CodeSmellLookup.FirstOverlapping(smells, 7, 10);

        Assert.IsNotNull(result);
    }
}
