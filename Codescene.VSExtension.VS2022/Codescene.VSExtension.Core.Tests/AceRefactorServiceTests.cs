// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Ace;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
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

    [TestMethod]
    public void GetRefactorableFunction_FindsMatchingFunction_ByCategoryAndLine() =>
       AssertFindsExpectedFunction(
           "TargetFunction",
           CreateCodeSmell("Complex Method", 10),
           CreateRefactorableFunction("TargetFunction", "Complex Method", 10),
           CreateRefactorableFunction("OtherFunction", "Other Issue", 20));

    [TestMethod]
    public void GetRefactorableFunction_ReturnsNull_WhenNoMatch() =>
        Assert.IsNull(FindRefactorableFunction(
            CreateCodeSmell("Nonexistent Issue", 999),
            CreateRefactorableFunction("SomeFunction", "Complex Method", 10)));

    [TestMethod]
    public void GetRefactorableFunction_ReturnsNull_WhenEmptyList() =>
        Assert.IsNull(FindRefactorableFunction(CreateCodeSmell("Complex Method", 10)));

    [TestMethod]
    public void GetRefactorableFunction_MatchesFirstOccurrence_WhenMultipleMatches() =>
        AssertFindsExpectedFunction(
            "FirstMatch",
            CreateCodeSmell("Complex Method", 10),
            CreateRefactorableFunction("FirstMatch", "Complex Method", 10),
            CreateRefactorableFunction("SecondMatch", "Complex Method", 10));

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

    [TestMethod]
    public void CheckContainsRefactorableFunctions_EmptyFilePath_ReturnsEmptyList()
    {
        // Arrange
        var fileReview = CreateFileReviewModel(filePath: string.Empty);

        // Act
        var result = _aceRefactorService.CheckContainsRefactorableFunctions(fileReview, "code");

        // Assert
        Assert.IsEmpty(result);
        _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("Invalid file name"))), Times.Once);
    }

    [TestMethod]
    public void CheckContainsRefactorableFunctions_WhitespaceFilePath_ReturnsEmptyList()
    {
        // Arrange
        var fileReview = CreateFileReviewModel(filePath: "   ");

        // Act
        var result = _aceRefactorService.CheckContainsRefactorableFunctions(fileReview, "code");

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void CheckContainsRefactorableFunctions_UnsupportedExtension_ReturnsEmptyList()
    {
        // Arrange
        var fileReview = CreateFileReviewModel(filePath: "test.unsupported");
        _mockPreflightManager.Setup(x => x.IsSupportedLanguage("unsupported")).Returns(false);

        // Act
        var result = _aceRefactorService.CheckContainsRefactorableFunctions(fileReview, "code");

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void CheckContainsRefactorableFunctions_ExceptionThrown_ReturnsEmptyListAndLogsError()
    {
        SetupSupportedLanguage();
        SetupAceManagerThrows(new Exception("Test exception"));

        var result = _aceRefactorService.CheckContainsRefactorableFunctions(CreateFileReviewModel(), "code");

        Assert.IsEmpty(result);
        _mockLogger.Verify(l => l.Error(It.Is<string>(s => s.Contains("Error checking refactorable functions")), It.IsAny<Exception>()), Times.Once);
    }

    [TestMethod]
    public void CheckContainsRefactorableFunctions_NoRefactorableFunctions_ReturnsEmptyList()
    {
        SetupSupportedLanguage();
        SetupAceManagerReturns(new List<FnToRefactorModel>());

        var result = _aceRefactorService.CheckContainsRefactorableFunctions(CreateFileReviewModel(), "code");

        Assert.IsEmpty(result);
        _mockLogger.Verify(l => l.Warn(It.Is<string>(s => s.Contains("No refactorable functions found"))), Times.Once);
    }

    [TestMethod]
    public void CheckContainsRefactorableFunctions_RefactorableFunctionsFound_ReturnsTheFunctions()
    {
        SetupSupportedLanguage();
        var refactorableFunctions = new List<FnToRefactorModel>
        {
            CreateRefactorableFunction("Function1", "Complex Method", 10),
            CreateRefactorableFunction("Function2", "Deep Nesting", 25),
        };
        SetupAceManagerReturns(refactorableFunctions);

        var result = _aceRefactorService.CheckContainsRefactorableFunctions(CreateFileReviewModel(), "code");

        Assert.HasCount(2, result);
        Assert.AreEqual("Function1", result[0].Name);
        Assert.AreEqual("Function2", result[1].Name);
    }

    [TestMethod]
    public void CheckContainsRefactorableFunctions_RefactorableFunctionsFound_LogsInfo()
    {
        SetupSupportedLanguage();
        SetupAceManagerReturns(new List<FnToRefactorModel> { CreateRefactorableFunction("Function1", "Complex Method", 10) });

        _aceRefactorService.CheckContainsRefactorableFunctions(CreateFileReviewModel(), "code");

        _mockLogger.Verify(l => l.Info(It.Is<string>(s => s.Contains("Found 1 refactorable function"))), Times.Once);
    }

    [TestMethod]
    public void CheckContainsRefactorableFunctions_MapsCodeSmellsToCliModel()
    {
        var codeSmell = CreateCodeSmell("Complex Method", 10);
        var fileReview = new FileReviewModel
        {
            FilePath = "test.cs",
            Score = 7.5f,
            FileLevel = new List<CodeSmellModel>(),
            FunctionLevel = new List<CodeSmellModel> { codeSmell },
        };
        SetupSupportedLanguage();
        _mockMapper.Setup(x => x.Map(codeSmell)).Returns(new CliCodeSmellModel { Category = "Complex Method" });
        SetupAceManagerReturns(new List<FnToRefactorModel>());

        _aceRefactorService.CheckContainsRefactorableFunctions(fileReview, "code");

        _mockMapper.Verify(m => m.Map(codeSmell), Times.Once);
    }

    [TestMethod]
    public void CheckContainsRefactorableFunctions_CallsGetPreflightResponse()
    {
        SetupSupportedLanguage();
        SetupAceManagerReturns(new List<FnToRefactorModel>());

        _aceRefactorService.CheckContainsRefactorableFunctions(CreateFileReviewModel(), "code");

        _mockPreflightManager.Verify(p => p.GetPreflightResponse(), Times.AtLeast(1));
    }

    private static CodeSmellModel CreateCodeSmell(string category, int startLine) =>
     new CodeSmellModel { Category = category, Range = new CodeRangeModel(startLine, startLine + 10, 1, 50) };

    private static FnToRefactorModel CreateRefactorableFunction(string name, string category, int line) =>
        new FnToRefactorModel { Name = name, RefactoringTargets = new[] { new RefactoringTargetModel { Category = category, Line = line } } };

    private static FileReviewModel CreateFileReviewModel(string filePath = "test.cs") =>
        new FileReviewModel
        {
            FilePath = filePath,
            Score = 7.5f,
            FileLevel = new List<CodeSmellModel>(),
            FunctionLevel = new List<CodeSmellModel>(),
        };

    private void SetupSupportedLanguage(string extension = "cs") =>
    _mockPreflightManager.Setup(x => x.IsSupportedLanguage(extension)).Returns(true);

    private void SetupAceManagerReturns(IList<FnToRefactorModel> functions) =>
        _mockAceManager.Setup(x => x.GetRefactorableFunctionsFromCodeSmells(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<CliCodeSmellModel>>(),
            It.IsAny<PreFlightResponseModel>()))
            .Returns(functions);

    private void SetupAceManagerThrows(Exception ex) =>
        _mockAceManager.Setup(x => x.GetRefactorableFunctionsFromCodeSmells(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<List<CliCodeSmellModel>>(),
            It.IsAny<PreFlightResponseModel>()))
            .Throws(ex);

    private FnToRefactorModel FindRefactorableFunction(CodeSmellModel smell, params FnToRefactorModel[] functions) =>
        _aceRefactorService.GetRefactorableFunction(smell, functions.ToList());

    private void AssertFindsExpectedFunction(string expectedName, CodeSmellModel smell, params FnToRefactorModel[] functions)
    {
        var result = FindRefactorableFunction(smell, functions);
        Assert.IsNotNull(result);
        Assert.AreEqual(expectedName, result.Name);
    }
}
