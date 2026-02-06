// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CodeSmellDocumentationMapperTests
    {
        private CodeSmellDocumentationMapper _mapper;
        private Mock<ISettingsProvider> _mockSettingsProvider;

        [TestInitialize]
        public void Setup()
        {
            _mockSettingsProvider = new Mock<ISettingsProvider>();
            _mockSettingsProvider.Setup(x => x.AuthToken).Returns("valid-token");
            _mapper = new CodeSmellDocumentationMapper(_mockSettingsProvider.Object);
        }

        [TestMethod]
        public void Map_WithValidModel_ReturnsComponentData()
        {
            var result = _mapper.Map(CreateModel(), CreateFnToRefactor());
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void Map_SetsDocTypeWithDocsPrefix()
        {
            var model = CreateModel(category: "Complex Method");

            var result = _mapper.Map(model, CreateFnToRefactor());

            Assert.AreEqual("docs_issues_complex_method", result.DocType);
        }

        [TestMethod]
        public void Map_CategoryAlreadyHasDocsPrefix_DoesNotAddPrefixAgain()
        {
            var model = CreateModel(category: "docs_issues_complex_method");

            var result = _mapper.Map(model, CreateFnToRefactor());

            Assert.AreEqual("docs_issues_complex_method", result.DocType);
        }

        [TestMethod]
        public void Map_CategoryWithHyphens_ConvertedToSnakeCase()
        {
            var model = CreateModel(category: "Bumpy-Road-Ahead");

            var result = _mapper.Map(model, CreateFnToRefactor());

            Assert.AreEqual("docs_issues_bumpy_road_ahead", result.DocType);
        }

        [TestMethod]
        public void Map_NullFunctionName_DefaultsToEmptyString()
        {
            var model = CreateModel(functionName: null);

            var result = _mapper.Map(model, CreateFnToRefactor());

            Assert.AreEqual(string.Empty, result.FileData.Fn.Name);
        }

        [TestMethod]
        public void Map_ValidRange_MappedCorrectly()
        {
            var range = new CodeRangeModel(10, 25, 5, 80);
            var result = _mapper.Map(CreateModel(range: range), CreateFnToRefactor());

            Assert.AreEqual(10, result.FileData.Fn.Range.StartLine);
            Assert.AreEqual(25, result.FileData.Fn.Range.EndLine);
            Assert.AreEqual(5, result.FileData.Fn.Range.StartColumn);
            Assert.AreEqual(80, result.FileData.Fn.Range.EndColumn);
        }

        [TestMethod]
        public void Map_SetsFileNameFromPath()
        {
            var model = CreateModel(path: "src/components/MyComponent.cs");

            var result = _mapper.Map(model, CreateFnToRefactor());

            Assert.AreEqual("src/components/MyComponent.cs", result.FileData.FileName);
        }

        [TestMethod]
        public void Map_SetsFnToRefactorInFileData()
        {
            var fnToRefactor = CreateFnToRefactor("MyFunction");

            var result = _mapper.Map(CreateModel(), fnToRefactor);

            Assert.AreEqual(fnToRefactor, result.FileData.FnToRefactor);
        }

        [TestMethod]
        public void Map_NullFnToRefactor_DisablesAutoRefactor()
        {
            var result = _mapper.Map(CreateModel(), null);

            Assert.IsTrue(result.AutoRefactor.Disabled);
        }

        [TestMethod]
        public void Map_ValidFnToRefactor_EnablesAutoRefactor()
        {
            var result = _mapper.Map(CreateModel(), CreateFnToRefactor());

            Assert.IsFalse(result.AutoRefactor.Disabled);
        }

        [TestMethod]
        public void Map_NoAuthToken_DisablesAutoRefactor()
        {
            // Arrange
            SetupAuthToken(string.Empty);

            // Act
            var result = _mapper.Map(CreateModel(), CreateFnToRefactor());

            // Assert
            Assert.IsTrue(result.AutoRefactor.Disabled);
        }

        [TestMethod]
        public void Map_NullAuthToken_DisablesAutoRefactor()
        {
            // Arrange
            SetupAuthToken(null);

            // Act
            var result = _mapper.Map(CreateModel(), CreateFnToRefactor());

            // Assert
            Assert.IsTrue(result.AutoRefactor.Disabled);
        }

        [TestMethod]
        public void Map_WhitespaceAuthToken_DisablesAutoRefactor()
        {
            // Arrange
            SetupAuthToken("   ");

            // Act
            var result = _mapper.Map(CreateModel(), CreateFnToRefactor());

            // Assert
            Assert.IsTrue(result.AutoRefactor.Disabled);
        }

        [TestMethod]
        public void Map_ValidAuthTokenAndFnToRefactor_EnablesAutoRefactor()
        {
            // Arrange
            SetupAuthToken("valid-token");

            // Act
            var result = _mapper.Map(CreateModel(), CreateFnToRefactor());

            // Assert
            Assert.IsFalse(result.AutoRefactor.Disabled);
        }

        [TestMethod]
        public void Map_ValidAuthTokenButNullFnToRefactor_DisablesAutoRefactor()
        {
            // Arrange
            SetupAuthToken("valid-token");

            // Act
            var result = _mapper.Map(CreateModel(), null);

            // Assert
            Assert.IsTrue(result.AutoRefactor.Disabled);
        }

        [TestMethod]
        public void Map_NoAuthTokenAndNullFnToRefactor_DisablesAutoRefactor()
        {
            // Arrange
            SetupAuthToken(string.Empty);

            // Act
            var result = _mapper.Map(CreateModel(), null);

            // Assert
            Assert.IsTrue(result.AutoRefactor.Disabled);
        }

        [TestMethod]
        public void Map_AceAcknowledgedTrue_SetsActivatedTrue()
        {
            var result = _mapper.Map(CreateModel(), CreateFnToRefactor(), aceAcknowledged: true);

            Assert.IsTrue(result.AutoRefactor.Activated);
        }

        [TestMethod]
        public void Map_AceAcknowledgedFalse_SetsActivatedFalse()
        {
            var result = _mapper.Map(CreateModel(), CreateFnToRefactor(), aceAcknowledged: false);

            Assert.IsFalse(result.AutoRefactor.Activated);
        }

        [TestMethod]
        public void Map_AutoRefactorVisibleAlwaysTrue()
        {
            var result = _mapper.Map(CreateModel(), CreateFnToRefactor());

            Assert.IsTrue(result.AutoRefactor.Visible);
        }

        private static ShowDocumentationModel CreateModel(
         string path = "test.cs",
         string category = "Complex Method",
         string functionName = "TestFunction",
         CodeRangeModel? range = null)
        {
            return new ShowDocumentationModel(
                path,
                category,
                functionName,
                range ?? new CodeRangeModel(10, 20, 1, 50));
        }

        private static FnToRefactorModel CreateFnToRefactor(string name = "TestFunction") =>
           new FnToRefactorModel { Name = name };

        private void SetupAuthToken(string token) =>
           _mockSettingsProvider.Setup(x => x.AuthToken).Returns(token);
    }
}
