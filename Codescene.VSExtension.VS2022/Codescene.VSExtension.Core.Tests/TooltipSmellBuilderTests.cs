// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application;
using Codescene.VSExtension.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class TooltipSmellBuilderTests
{
    [TestMethod]
    public void BuildsSmellFromTooltipData()
    {
        var category = "Complex Method";
        var range = new CodeRangeModel(5, 10, 1, 20);
        var functionName = "Calculate";
        var functionRange = new CodeRangeModel(3, 15, 1, 5);

        var result = TooltipSmellBuilder.Build(category, range, functionName, functionRange);

        Assert.IsNotNull(result);
        Assert.AreEqual("Complex Method", result.Category);
        Assert.AreEqual(5, result.Range.StartLine);
        Assert.AreEqual(10, result.Range.EndLine);
        Assert.AreEqual(1, result.Range.StartColumn);
        Assert.AreEqual(20, result.Range.EndColumn);
        Assert.AreEqual("Calculate", result.FunctionName);
        Assert.IsNotNull(result.FunctionRange);
        Assert.AreEqual(3, result.FunctionRange.StartLine);
        Assert.AreEqual(15, result.FunctionRange.EndLine);
        Assert.AreEqual(1, result.FunctionRange.StartColumn);
        Assert.AreEqual(5, result.FunctionRange.EndColumn);
    }

    [TestMethod]
    public void FunctionRange_SetWhenProvided()
    {
        var category = "Brain Method";
        var range = new CodeRangeModel(1, 1, 1, 1);
        var functionRange = new CodeRangeModel(1, 50, 1, 10);

        var result = TooltipSmellBuilder.Build(category, range, "MyFunc", functionRange);

        Assert.IsNotNull(result.FunctionRange);
        Assert.AreEqual(1, result.FunctionRange.StartLine);
        Assert.AreEqual(50, result.FunctionRange.EndLine);
    }

    [TestMethod]
    public void NullFunctionRange_PreservedAsNull()
    {
        var result = TooltipSmellBuilder.Build("Large File", new CodeRangeModel(1, 100, 1, 1), null, null);

        Assert.IsNull(result.FunctionRange);
        Assert.IsNull(result.FunctionName);
    }

    [TestMethod]
    public void EmptyFunctionName_PreservedAsEmpty()
    {
        var result = TooltipSmellBuilder.Build("Category", new CodeRangeModel(1, 1, 1, 1), string.Empty, null);

        Assert.AreEqual(string.Empty, result.FunctionName);
    }
}
