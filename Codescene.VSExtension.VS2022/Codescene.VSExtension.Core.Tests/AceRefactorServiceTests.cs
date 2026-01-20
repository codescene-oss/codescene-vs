using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Mapper;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Moq;

namespace Codescene.VSExtension.Core.Tests;

[TestClass]
public class AceRefactorServiceTests
{
    private Mock<IAceManager> _mockAceManager;
    private Mock<IPreflightManager> _mockPreflightManager;
    private Mock<IModelMapper> _mockMapper;
    private Mock<ILogger> _mockLogger;
    private AceRefactorService _aceRefactorService;

    [TestInitialize]
    public void Setup()
    {
        _mockAceManager = new Mock<IAceManager>();
        _mockPreflightManager = new Mock<IPreflightManager>();
        _mockMapper = new Mock<IModelMapper>();
        _mockLogger = new Mock<ILogger>();

        _aceRefactorService = new AceRefactorService(
            _mockAceManager.Object,
            _mockPreflightManager.Object,
            _mockMapper.Object,
            _mockLogger.Object);
    }

    #region GetRefactorableFunction Tests

    private static CodeSmellModel CreateCodeSmell(string category, int startLine) =>
        new CodeSmellModel { Category = category, Range = new CodeSmellRangeModel(startLine, startLine + 10, 1, 50) };

    private static FnToRefactorModel CreateRefactorableFunction(string name, string category, int line) =>
        new FnToRefactorModel { Name = name, RefactoringTargets = new[] { new RefactoringTargetModel { Category = category, Line = line } } };

    private FnToRefactorModel FindRefactorableFunction(CodeSmellModel smell, params FnToRefactorModel[] functions) =>
        _aceRefactorService.GetRefactorableFunction(smell, functions.ToList());

    private void AssertFindsExpectedFunction(string expectedName, CodeSmellModel smell, params FnToRefactorModel[] functions)
    {
        var result = FindRefactorableFunction(smell, functions);
        Assert.IsNotNull(result);
        Assert.AreEqual(expectedName, result.Name);
    }

    [TestMethod]
    public void GetRefactorableFunction_FindsMatchingFunction_ByCategoryAndLine() =>
        AssertFindsExpectedFunction("TargetFunction", CreateCodeSmell("Complex Method", 10),
            CreateRefactorableFunction("TargetFunction", "Complex Method", 10),
            CreateRefactorableFunction("OtherFunction", "Other Issue", 20));

    [TestMethod]
    public void GetRefactorableFunction_ReturnsNull_WhenNoMatch() =>
        Assert.IsNull(FindRefactorableFunction(CreateCodeSmell("Nonexistent Issue", 999),
            CreateRefactorableFunction("SomeFunction", "Complex Method", 10)));

    [TestMethod]
    public void GetRefactorableFunction_ReturnsNull_WhenEmptyList() =>
        Assert.IsNull(FindRefactorableFunction(CreateCodeSmell("Complex Method", 10)));

    [TestMethod]
    public void GetRefactorableFunction_MatchesFirstOccurrence_WhenMultipleMatches() =>
        AssertFindsExpectedFunction("FirstMatch", CreateCodeSmell("Complex Method", 10),
            CreateRefactorableFunction("FirstMatch", "Complex Method", 10),
            CreateRefactorableFunction("SecondMatch", "Complex Method", 10));

    #endregion

    #region ShouldCheckRefactorableFunctions Tests

    [TestMethod]
    public void ShouldCheckRefactorableFunctions_ReturnsTrue_WhenLanguageSupported()
    {
        // Arrange
        _mockPreflightManager.Setup(x => x.IsSupportedLanguage("cs")).Returns(true);

        // Act
        var result = _aceRefactorService.ShouldCheckRefactorableFunctions("cs");

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldCheckRefactorableFunctions_ReturnsFalse_WhenLanguageNotSupported()
    {
        // Arrange
        _mockPreflightManager.Setup(x => x.IsSupportedLanguage("unsupported")).Returns(false);

        // Act
        var result = _aceRefactorService.ShouldCheckRefactorableFunctions("unsupported");

        // Assert
        Assert.IsFalse(result);
        _mockLogger.Verify(l => l.Debug(It.Is<string>(s => s.Contains("not supported"))), Times.Once);
    }

    #endregion
}
