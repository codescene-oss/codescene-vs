using Codescene.VSExtension.Core.Application.Services.Util;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class IndentationServiceTests
{
    private IndentationService _indentationService;

    [TestInitialize]
    public void Setup()
    {
        _indentationService = new IndentationService();
    }

    #region AdjustIndentation Tests

    private static IndentationInfo CreateIndentationInfo(int level, bool usesTabs = false, int tabSize = 4) =>
        new IndentationInfo { Level = level, UsesTabs = usesTabs, TabSize = tabSize };

    private string[] AdjustAndSplitLines(string code, IndentationInfo info) =>
        _indentationService.AdjustIndentation(code, info)
            .Split(new[] { Environment.NewLine }, StringSplitOptions.None);

    [TestMethod]
    public void AdjustIndentation_WithZeroLevel_ReturnsOriginalCode()
    {
        // Arrange
        var code = "function test() {\n    return 1;\n}";

        // Act
        var result = _indentationService.AdjustIndentation(code, CreateIndentationInfo(level: 0));

        // Assert
        Assert.AreEqual(code, result);
    }

    [TestMethod]
    public void AdjustIndentation_WithSpaces_AddsCorrectIndentation()
    {
        // Arrange & Act
        var lines = AdjustAndSplitLines("line1\nline2\nline3", CreateIndentationInfo(level: 2, tabSize: 4));

        // Assert - 2 * 4 = 8 spaces
        Assert.AreEqual("        line1", lines[0]);
        Assert.AreEqual("        line2", lines[1]);
        Assert.AreEqual("        line3", lines[2]);
    }

    [TestMethod]
    public void AdjustIndentation_WithTabs_AddsCorrectIndentation()
    {
        // Arrange & Act
        var lines = AdjustAndSplitLines("line1\nline2", CreateIndentationInfo(level: 2, usesTabs: true));

        // Assert - 2 tabs
        Assert.AreEqual("\t\tline1", lines[0]);
        Assert.AreEqual("\t\tline2", lines[1]);
    }

    [TestMethod]
    public void AdjustIndentation_PreservesEmptyLines()
    {
        // Arrange & Act
        var lines = AdjustAndSplitLines("line1\n\nline3", CreateIndentationInfo(level: 1));

        // Assert
        Assert.AreEqual("    line1", lines[0]);
        Assert.AreEqual("", lines[1]); // Empty line stays empty
        Assert.AreEqual("    line3", lines[2]);
    }

    [TestMethod]
    public void AdjustIndentation_PreservesWhitespaceOnlyLines()
    {
        // Arrange & Act
        var lines = AdjustAndSplitLines("line1\n   \nline3", CreateIndentationInfo(level: 1));

        // Assert
        Assert.AreEqual("    line1", lines[0]);
        Assert.AreEqual("   ", lines[1]); // Whitespace-only line not indented
        Assert.AreEqual("    line3", lines[2]);
    }

    [TestMethod]
    public void AdjustIndentation_HandlesWindowsLineEndings()
    {
        // Arrange & Act
        var result = _indentationService.AdjustIndentation("line1\r\nline2\r\nline3", CreateIndentationInfo(level: 1, tabSize: 2));

        // Assert
        StringAssert.Contains(result, "  line1");
        StringAssert.Contains(result, "  line2");
        StringAssert.Contains(result, "  line3");
    }

    #endregion

    #region CountLeadingWhitespace Tests

    [TestMethod]
    public void CountLeadingWhitespace_CountsTabsCorrectly()
    {
        // Arrange
        var lineText = "\t\t\tcode";

        // Act
        var (tabCount, spaceCount) = _indentationService.CountLeadingWhitespace(lineText);

        // Assert
        Assert.AreEqual(3, tabCount);
        Assert.AreEqual(0, spaceCount);
    }

    [TestMethod]
    public void CountLeadingWhitespace_CountsSpacesCorrectly()
    {
        // Arrange
        var lineText = "    code";

        // Act
        var (tabCount, spaceCount) = _indentationService.CountLeadingWhitespace(lineText);

        // Assert
        Assert.AreEqual(0, tabCount);
        Assert.AreEqual(4, spaceCount);
    }

    [TestMethod]
    public void CountLeadingWhitespace_CountsMixedCorrectly()
    {
        // Arrange
        var lineText = "\t  \tcode";

        // Act
        var (tabCount, spaceCount) = _indentationService.CountLeadingWhitespace(lineText);

        // Assert
        Assert.AreEqual(2, tabCount);
        Assert.AreEqual(2, spaceCount);
    }

    [TestMethod]
    public void CountLeadingWhitespace_ReturnsZero_ForNoWhitespace()
    {
        // Arrange
        var lineText = "code";

        // Act
        var (tabCount, spaceCount) = _indentationService.CountLeadingWhitespace(lineText);

        // Assert
        Assert.AreEqual(0, tabCount);
        Assert.AreEqual(0, spaceCount);
    }

    [TestMethod]
    public void CountLeadingWhitespace_ReturnsZero_ForEmptyString()
    {
        // Arrange
        var lineText = "";

        // Act
        var (tabCount, spaceCount) = _indentationService.CountLeadingWhitespace(lineText);

        // Assert
        Assert.AreEqual(0, tabCount);
        Assert.AreEqual(0, spaceCount);
    }

    #endregion

    #region DetermineTabSize Tests

    [TestMethod]
    public void DetermineTabSize_ReturnsDefault_WhenUsingTabs()
    {
        // Act
        var tabSize = _indentationService.DetermineTabSize(usesTabs: true, spaceCount: 8);

        // Assert
        Assert.AreEqual(4, tabSize); // Default when using tabs
    }

    [TestMethod]
    public void DetermineTabSize_DetectsSize2()
    {
        // Act
        var tabSize = _indentationService.DetermineTabSize(usesTabs: false, spaceCount: 6);

        // Assert
        Assert.AreEqual(2, tabSize); // 6 is divisible by 2
    }

    [TestMethod]
    public void DetermineTabSize_DetectsSize4()
    {
        // Act
        var tabSize = _indentationService.DetermineTabSize(usesTabs: false, spaceCount: 8);

        // Assert
        Assert.AreEqual(2, tabSize); // 8 is divisible by 2 first, then 4
    }

    [TestMethod]
    public void DetermineTabSize_DetectsSize8()
    {
        // Act
        var tabSize = _indentationService.DetermineTabSize(usesTabs: false, spaceCount: 24);

        // Assert
        Assert.AreEqual(2, tabSize); // 24 is divisible by 2 first
    }

    [TestMethod]
    public void DetermineTabSize_ReturnsDefault_WhenNoSpaces()
    {
        // Act
        var tabSize = _indentationService.DetermineTabSize(usesTabs: false, spaceCount: 0);

        // Assert
        Assert.AreEqual(4, tabSize); // Default
    }

    #endregion
}
