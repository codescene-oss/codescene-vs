// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Util;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class PerformanceTelemetryHelperTests
    {
        private Mock<ITelemetryManager> _mockTelemetryManager;
        private Mock<ILogger> _mockLogger;

        [TestInitialize]
        public void Setup()
        {
            _mockTelemetryManager = new Mock<ITelemetryManager>();
            _mockLogger = new Mock<ILogger>();
        }

        [TestMethod]
        public void SendPerformanceTelemetry_ValidData_SendsTelemetryWithCorrectEventName()
        {
            // Arrange
            var data = new PerformanceTelemetryData
            {
                Type = Constants.Titles.REVIEW,
                ElapsedMs = 150,
                FilePath = "test.cs",
                Loc = 100,
                Language = "cs",
            };

            // Act
            PerformanceTelemetryHelper.SendPerformanceTelemetry(
                _mockTelemetryManager.Object,
                _mockLogger.Object,
                data);

            // Assert
            _mockTelemetryManager.Verify(
                t => t.SendTelemetry(
                    Constants.Telemetry.ANALYSISPERFORMANCE,
                    It.IsAny<Dictionary<string, object>>()),
                Times.Once);
        }

        [TestMethod]
        public void SendPerformanceTelemetry_ValidData_IncludesAllRequiredFields()
        {
            // Arrange
            var data = new PerformanceTelemetryData
            {
                Type = Constants.Titles.DELTA,
                ElapsedMs = 250,
                FilePath = "example.js",
                Loc = 200,
                Language = "javascript",
            };

            Dictionary<string, object> capturedData = null;
            _mockTelemetryManager
                .Setup(t => t.SendTelemetry(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Callback<string, Dictionary<string, object>>((_, d) => capturedData = d);

            // Act
            PerformanceTelemetryHelper.SendPerformanceTelemetry(
                _mockTelemetryManager.Object,
                _mockLogger.Object,
                data);

            // Assert
            Assert.IsNotNull(capturedData);
            Assert.AreEqual(Constants.Titles.DELTA, capturedData["type"]);
            Assert.AreEqual(250L, capturedData["elapsedMs"]);
            Assert.AreEqual("javascript", capturedData["language"]);
            Assert.AreEqual(Constants.Telemetry.SOURCEIDE, capturedData["editor-type"]);
            Assert.AreEqual(200, capturedData["loc"]);
        }

        [TestMethod]
        public void SendPerformanceTelemetry_NullLanguage_UsesEmptyString()
        {
            // Arrange
            var data = new PerformanceTelemetryData
            {
                Type = Constants.Titles.ACE,
                ElapsedMs = 300,
                Language = null,
                Loc = 50,
            };

            Dictionary<string, object> capturedData = null;
            _mockTelemetryManager
                .Setup(t => t.SendTelemetry(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Callback<string, Dictionary<string, object>>((_, d) => capturedData = d);

            // Act
            PerformanceTelemetryHelper.SendPerformanceTelemetry(
                _mockTelemetryManager.Object,
                _mockLogger.Object,
                data);

            // Assert
            Assert.IsNotNull(capturedData);
            Assert.AreEqual(string.Empty, capturedData["language"]);
        }

        [TestMethod]
        public void SendPerformanceTelemetry_NullTelemetryManager_DoesNotThrow()
        {
            // Arrange
            var data = new PerformanceTelemetryData
            {
                Type = Constants.Titles.REVIEW,
                ElapsedMs = 100,
            };

            // Act & Assert - should not throw
            PerformanceTelemetryHelper.SendPerformanceTelemetry(
                null,
                _mockLogger.Object,
                data);
        }

        [TestMethod]
        public void SendPerformanceTelemetry_NullData_DoesNotThrow()
        {
            // Act & Assert - should not throw
            PerformanceTelemetryHelper.SendPerformanceTelemetry(
                _mockTelemetryManager.Object,
                _mockLogger.Object,
                null);

            // Assert - should not send telemetry
            _mockTelemetryManager.Verify(
                t => t.SendTelemetry(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()),
                Times.Never);
        }

        [TestMethod]
        public void SendPerformanceTelemetry_ExceptionThrown_LogsErrorAndDoesNotThrow()
        {
            // Arrange
            var data = new PerformanceTelemetryData
            {
                Type = Constants.Titles.REVIEW,
                ElapsedMs = 100,
            };

            _mockTelemetryManager
                .Setup(t => t.SendTelemetry(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
                .Throws(new Exception("Telemetry error"));

            // Act & Assert - should not throw
            PerformanceTelemetryHelper.SendPerformanceTelemetry(
                _mockTelemetryManager.Object,
                _mockLogger.Object,
                data);

            // Assert - should log error
            _mockLogger.Verify(
                l => l.Debug(It.Is<string>(s => s.Contains("Failed to send performance telemetry"))),
                Times.Once);
        }

        [TestMethod]
        public void SendPerformanceTelemetry_NullLogger_DoesNotThrow()
        {
            // Arrange
            var data = new PerformanceTelemetryData
            {
                Type = Constants.Titles.REVIEW,
                ElapsedMs = 100,
            };

            // Act & Assert - should not throw
            PerformanceTelemetryHelper.SendPerformanceTelemetry(
                _mockTelemetryManager.Object,
                null,
                data);
        }

        [TestMethod]
        public void CalculateLineCount_NullContent_ReturnsZero()
        {
            // Act
            var result = PerformanceTelemetryHelper.CalculateLineCount(null);

            // Assert
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void CalculateLineCount_EmptyContent_ReturnsZero()
        {
            // Act
            var result = PerformanceTelemetryHelper.CalculateLineCount(string.Empty);

            // Assert
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void CalculateLineCount_SingleLine_ReturnsOne()
        {
            // Arrange
            var content = "single line";

            // Act
            var result = PerformanceTelemetryHelper.CalculateLineCount(content);

            // Assert
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void CalculateLineCount_MultipleLinesWithNewline_ReturnsCorrectCount()
        {
            // Arrange
            var content = "line1\nline2\nline3";

            // Act
            var result = PerformanceTelemetryHelper.CalculateLineCount(content);

            // Assert
            Assert.AreEqual(3, result);
        }

        [TestMethod]
        public void CalculateLineCount_MultipleLinesWithCarriageReturn_ReturnsCorrectCount()
        {
            // Arrange
            var content = "line1\rline2\rline3";

            // Act
            var result = PerformanceTelemetryHelper.CalculateLineCount(content);

            // Assert
            Assert.AreEqual(3, result);
        }

        [TestMethod]
        public void CalculateLineCount_MultipleLinesWithWindowsLineEnding_ReturnsCorrectCount()
        {
            // Arrange
            var content = "line1\r\nline2\r\nline3";

            // Act
            var result = PerformanceTelemetryHelper.CalculateLineCount(content);

            // Assert
            Assert.AreEqual(3, result);
        }

        [TestMethod]
        public void CalculateLineCount_MixedLineEndings_ReturnsCorrectCount()
        {
            // Arrange
            var content = "line1\nline2\rline3\r\nline4";

            // Act
            var result = PerformanceTelemetryHelper.CalculateLineCount(content);

            // Assert
            Assert.AreEqual(4, result);
        }

        [TestMethod]
        public void CalculateLineCount_EmptyLines_CountsAllLines()
        {
            // Arrange
            var content = "line1\n\nline3\n";

            // Act
            var result = PerformanceTelemetryHelper.CalculateLineCount(content);

            // Assert
            Assert.AreEqual(4, result);
        }

        // ExtractLanguage Tests
        [TestMethod]
        public void ExtractLanguage_WithFnToRefactor_ReturnsFileTypeFromModel()
        {
            // Arrange
            var fnToRefactor = new FnToRefactorModel
            {
                FileType = "typescript",
            };

            // Act
            var result = PerformanceTelemetryHelper.ExtractLanguage("test.cs", fnToRefactor);

            // Assert
            Assert.AreEqual("typescript", result);
        }

        [TestMethod]
        public void ExtractLanguage_WithFnToRefactorNullFileType_ReturnsEmptyString()
        {
            // Arrange
            var fnToRefactor = new FnToRefactorModel
            {
                FileType = null,
            };

            // Act
            var result = PerformanceTelemetryHelper.ExtractLanguage("test.cs", fnToRefactor);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void ExtractLanguage_WithFilePath_ReturnsExtension()
        {
            // Act
            var result = PerformanceTelemetryHelper.ExtractLanguage("test.cs");

            // Assert
            Assert.AreEqual("cs", result);
        }

        [TestMethod]
        public void ExtractLanguage_WithFilePathUpperCase_ReturnsLowerCaseExtension()
        {
            // Act
            var result = PerformanceTelemetryHelper.ExtractLanguage("test.CS");

            // Assert
            Assert.AreEqual("cs", result);
        }

        [TestMethod]
        public void ExtractLanguage_WithFilePathMultipleDots_ReturnsLastExtension()
        {
            // Act
            var result = PerformanceTelemetryHelper.ExtractLanguage("test.config.js");

            // Assert
            Assert.AreEqual("js", result);
        }

        [TestMethod]
        public void ExtractLanguage_WithFilePathNoExtension_ReturnsEmptyString()
        {
            // Act
            var result = PerformanceTelemetryHelper.ExtractLanguage("testfile");

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void ExtractLanguage_WithNullFilePath_ReturnsEmptyString()
        {
            // Act
            var result = PerformanceTelemetryHelper.ExtractLanguage(null);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void ExtractLanguage_WithEmptyFilePath_ReturnsEmptyString()
        {
            // Act
            var result = PerformanceTelemetryHelper.ExtractLanguage(string.Empty);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void ExtractLanguage_WithFilePathLeadingDot_RemovesDot()
        {
            // Act
            var result = PerformanceTelemetryHelper.ExtractLanguage("test..cs");

            // Assert
            Assert.AreEqual("cs", result);
        }

        [TestMethod]
        public void ExtractLanguage_WithoutFnToRefactorAndFilePath_ReturnsEmptyString()
        {
            // Act
            var result = PerformanceTelemetryHelper.ExtractLanguage(null);

            // Assert
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public void ExtractLanguage_WithVariousFileExtensions_ReturnsCorrectLanguage()
        {
            // Test various common file extensions
            var testCases = new Dictionary<string, string>
            {
                { "file.cs", "cs" },
                { "file.js", "js" },
                { "file.ts", "ts" },
                { "file.py", "py" },
                { "file.java", "java" },
                { "file.cpp", "cpp" },
                { "file.h", "h" },
                { "file.xml", "xml" },
                { "file.json", "json" },
            };

            foreach (var testCase in testCases)
            {
                // Act
                var result = PerformanceTelemetryHelper.ExtractLanguage(testCase.Key);

                // Assert
                Assert.AreEqual(testCase.Value, result, $"Failed for {testCase.Key}");
            }
        }
    }
}
