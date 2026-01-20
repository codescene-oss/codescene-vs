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

    [TestMethod]
    public void GetRefactorableFunction_FindsMatchingFunction_ByCategoryAndLine()
    {
        // Arrange
        var codeSmell = new CodeSmellModel
        {
            Category = "Complex Method",
            Range = new CodeSmellRangeModel(10, 20, 1, 50)
        };

        var refactorableFunctions = new List<FnToRefactorModel>
        {
            new FnToRefactorModel
            {
                Name = "TargetFunction",
                RefactoringTargets = new[]
                {
                    new RefactoringTargetModel { Category = "Complex Method", Line = 10 }
                }
            },
            new FnToRefactorModel
            {
                Name = "OtherFunction",
                RefactoringTargets = new[]
                {
                    new RefactoringTargetModel { Category = "Other Issue", Line = 20 }
                }
            }
        };

        // Act
        var result = _aceRefactorService.GetRefactorableFunction(codeSmell, refactorableFunctions);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("TargetFunction", result.Name);
    }

    [TestMethod]
    public void GetRefactorableFunction_ReturnsNull_WhenNoMatch()
    {
        // Arrange
        var codeSmell = new CodeSmellModel
        {
            Category = "Nonexistent Issue",
            Range = new CodeSmellRangeModel(999, 1000, 1, 50)
        };

        var refactorableFunctions = new List<FnToRefactorModel>
        {
            new FnToRefactorModel
            {
                Name = "SomeFunction",
                RefactoringTargets = new[]
                {
                    new RefactoringTargetModel { Category = "Complex Method", Line = 10 }
                }
            }
        };

        // Act
        var result = _aceRefactorService.GetRefactorableFunction(codeSmell, refactorableFunctions);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetRefactorableFunction_ReturnsNull_WhenEmptyList()
    {
        // Arrange
        var codeSmell = new CodeSmellModel
        {
            Category = "Complex Method",
            Range = new CodeSmellRangeModel(10, 20, 1, 50)
        };
        var refactorableFunctions = new List<FnToRefactorModel>();

        // Act
        var result = _aceRefactorService.GetRefactorableFunction(codeSmell, refactorableFunctions);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetRefactorableFunction_MatchesFirstOccurrence_WhenMultipleMatches()
    {
        // Arrange
        var codeSmell = new CodeSmellModel
        {
            Category = "Complex Method",
            Range = new CodeSmellRangeModel(10, 20, 1, 50)
        };

        var refactorableFunctions = new List<FnToRefactorModel>
        {
            new FnToRefactorModel
            {
                Name = "FirstMatch",
                RefactoringTargets = new[]
                {
                    new RefactoringTargetModel { Category = "Complex Method", Line = 10 }
                }
            },
            new FnToRefactorModel
            {
                Name = "SecondMatch",
                RefactoringTargets = new[]
                {
                    new RefactoringTargetModel { Category = "Complex Method", Line = 10 }
                }
            }
        };

        // Act
        var result = _aceRefactorService.GetRefactorableFunction(codeSmell, refactorableFunctions);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("FirstMatch", result.Name);
    }

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
