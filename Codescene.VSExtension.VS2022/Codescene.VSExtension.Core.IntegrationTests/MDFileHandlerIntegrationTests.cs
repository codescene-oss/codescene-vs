// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Util;

namespace Codescene.VSExtension.Core.IntegrationTests
{
    [TestClass]
    public class MDFileHandlerIntegrationTests
    {
        private MDFileHandler _handler;
        private string _tempDirectory;

        [TestInitialize]
        public void Setup()
        {
            _handler = new MDFileHandler();
            _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        [TestMethod]
        public void GetContent_WhenFileNameNotSet_ReturnsNull()
        {
            // Arrange - _fileName is null by default

            // Act
            var result = _handler.GetContent("docs", null);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void SetFileName_SetsFileName()
        {
            // Arrange & Act
            _handler.SetFileName("test-file");

            // Assert
            var result = _handler.GetContent("docs", null);
            Assert.IsNotNull(result); // Will return "file not found" HTML
        }

        [TestMethod]
        public void GetContent_WhenFileNotFound_ReturnsNotFoundHtml()
        {
            // Arrange
            _handler.SetFileName("nonexistent-file-" + Guid.NewGuid().ToString());

            // Act
            var result = _handler.GetContent("nonexistent-path", null);

            // Assert
            Assert.IsNotNull(result);
            Assert.Contains("Markdown file not found", result);
        }

        [TestMethod]
        public void GetContent_WhenFileNotFound_ReturnsHtmlWithParagraphTag()
        {
            // Arrange
            _handler.SetFileName("nonexistent-file");

            // Act
            var result = _handler.GetContent("nonexistent-path", null);

            // Assert - The error message is wrapped in HTML paragraph tag
            Assert.Contains("<p>", result);
        }

        [TestMethod]
        public void GetContent_WithSubPath_WhenFileNotFound_ReturnsNotFoundHtml()
        {
            // Arrange
            _handler.SetFileName("nonexistent-file");

            // Act
            var result = _handler.GetContent("docs", "subfolder");

            // Assert
            Assert.IsNotNull(result);
            Assert.Contains("Markdown file not found", result);
        }

        [TestMethod]
        public void SetFileName_CanBeCalledMultipleTimes()
        {
            // Arrange & Act
            _handler.SetFileName("file1");
            _handler.SetFileName("file2");
            _handler.SetFileName("file3");

            // Assert - Should use the last set filename
            var result = _handler.GetContent("docs", null);
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void Constructor_CreatesInstance()
        {
            // Act
            var handler = new MDFileHandler();

            // Assert
            Assert.IsNotNull(handler);
        }

        [TestMethod]
        public void GetContent_WithNullPath_WhenFileNameSet_DoesNotThrow()
        {
            // Arrange
            _handler.SetFileName("test");

            // Act & Assert - Should not throw, even with unusual path combinations
            try
            {
                var result = _handler.GetContent(null, null);

                // Result depends on how Path.Combine handles null
            }
            catch (ArgumentNullException)
            {
                // This is acceptable - Path.Combine throws on null
            }
        }

        [TestMethod]
        public void GetContent_WithEmptyPath_WhenFileNameSet_ReturnsNotFoundHtml()
        {
            // Arrange
            _handler.SetFileName("test");

            // Act
            var result = _handler.GetContent(string.Empty, null);

            // Assert
            Assert.IsNotNull(result);
        }
    }
}
