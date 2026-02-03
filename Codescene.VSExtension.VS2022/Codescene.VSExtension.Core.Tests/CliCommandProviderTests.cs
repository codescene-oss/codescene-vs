// Copyright (c) CodeScene. All rights reserved.

using System.Globalization;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Moq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class CliCommandProviderTests
    {
        private CliCommandProvider _commandProvider;
        private Mock<ILogger> _mockLogger;

        private const string TestFileName = "js";
        private const string TestFileContent = "content";
        private const string TestCachePath = "/home/user/cache";

        private static List<CliCodeSmellModel> CreateTestCodeSmells()
        {
            return new List<CliCodeSmellModel> { new CliCodeSmellModel { Category = "test" } };
        }

        private static PreFlightResponseModel CreateTestPreflight()
        {
            return new PreFlightResponseModel { FileTypes = new[] { ".js" } };
        }

        private static DeltaResponseModel CreateTestDeltaResult()
        {
            return new DeltaResponseModel { NewScore = 2, OldScore = 3 };
        }

        [TestInitialize]
        public void Initialize()
        {
            _mockLogger = new Mock<ILogger>();
            _commandProvider = new CliCommandProvider(new CliObjectScoreCreator(_mockLogger.Object));
        }

        [TestMethod]
        public void VersionCommand_ShouldReturnCorrectString()
        {
            var command = _commandProvider.VersionCommand;

            Assert.AreEqual("version --sha", command);
        }

        [TestMethod]
        public void GetReviewFileContentCommand_ShouldIncludeIdeApiAndFilename()
        {
            var command = _commandProvider.GetReviewFileContentCommand("testfile.txt");

            Assert.AreEqual("review --file-name testfile.txt", command);
        }

        [TestMethod]
        public void GetReviewPathCommand_ShouldIncludeIdeApiAndPath()
        {
            var command = _commandProvider.GetReviewPathCommand("some/path");

            Assert.AreEqual("review some/path", command);
        }

        [TestMethod]
        public void GetReviewFileContentPayload_ReturnsCorrectJson()
        {
            var payload = _commandProvider.GetReviewFileContentPayload(TestFileName, TestFileContent, TestCachePath);

            Assert.AreEqual($"{{\"path\":\"{TestFileName}\",\"file-content\":\"{TestFileContent}\",\"cache-path\":\"{TestCachePath}\"}}", payload);
        }

        [TestMethod]
        public void GetRefactorWithCodeSmellsPayload_Without_Preflight_ReturnsCorrectJson()
        {
            var codeSmells = CreateTestCodeSmells();

            var content = _commandProvider.GetRefactorWithCodeSmellsPayload(TestFileName, TestFileContent, TestCachePath, codeSmells, null);

            Assert.AreEqual($"{{\"code-smells\":[{{\"category\":\"{codeSmells.First().Category}\"}}],\"file-name\":\"{TestFileName}\",\"file-content\":\"{TestFileContent}\",\"cache-path\":\"{TestCachePath}\"}}", content);
        }

        [TestMethod]
        public void GetRefactorWithCodeSmellsPayload_With_Preflight_ReturnsCorrectJson()
        {
            var codeSmells = CreateTestCodeSmells();
            var preflight = CreateTestPreflight();

            var content = _commandProvider.GetRefactorWithCodeSmellsPayload(TestFileName, TestFileContent, TestCachePath, codeSmells, preflight);

            Assert.AreEqual($"{{\"code-smells\":[{{\"category\":\"{codeSmells.First().Category}\"}}],\"file-name\":\"{TestFileName}\",\"file-content\":\"{TestFileContent}\",\"preflight\":{{\"file-types\":[\"{preflight.FileTypes.First()}\"]}},\"cache-path\":\"{TestCachePath}\"}}", content);
        }

        [TestMethod]
        public void GetRefactorWithDeltaResultPayload_Without_Preflight_ReturnsCorrectJson()
        {
            var deltaResult = CreateTestDeltaResult();

            var content = _commandProvider.GetRefactorWithDeltaResultPayload(TestFileName, TestFileContent, TestCachePath, deltaResult, null);

            var expectedNewScore = deltaResult.NewScore.ToString("0.0", CultureInfo.InvariantCulture);
            var expectedOldScore = deltaResult.OldScore.ToString("0.0", CultureInfo.InvariantCulture);
            Assert.AreEqual($"{{\"delta-result\":{{\"new-score\":{expectedNewScore},\"old-score\":{expectedOldScore}}},\"file-name\":\"{TestFileName}\",\"file-content\":\"{TestFileContent}\",\"cache-path\":\"{TestCachePath}\"}}", content);
        }

        [TestMethod]
        public void GetRefactorWithDeltaResultPayload_With_Preflight_ReturnsCorrectJson()
        {
            var deltaResult = CreateTestDeltaResult();
            var preflight = CreateTestPreflight();

            var content = _commandProvider.GetRefactorWithDeltaResultPayload(TestFileName, TestFileContent, TestCachePath, deltaResult, preflight);

            var expectedNewScore = deltaResult.NewScore.ToString("0.0", CultureInfo.InvariantCulture);
            var expectedOldScore = deltaResult.OldScore.ToString("0.0", CultureInfo.InvariantCulture);
            Assert.AreEqual($"{{\"delta-result\":{{\"new-score\":{expectedNewScore},\"old-score\":{expectedOldScore}}},\"file-name\":\"{TestFileName}\",\"file-content\":\"{TestFileContent}\",\"preflight\":{{\"file-types\":[\"{preflight.FileTypes.First()}\"]}},\"cache-path\":\"{TestCachePath}\"}}", content);
        }

        [TestMethod]
        [DataRow(false, "refactor preflight")]
        [DataRow(true, "refactor preflight --force")]
        public void GetPreflightSupportInformationCommand_ReturnsCorrectCommand(bool force, string expected)
        {
            var command = _commandProvider.GetPreflightSupportInformationCommand(force);

            Assert.AreEqual(expected, command);
        }

        [TestMethod]
        public void SendTelemetryCommand_ReturnsCorrectFormat()
        {
            var jsonEvent = "{\"event\":\"test\"}";

            var command = _commandProvider.SendTelemetryCommand(jsonEvent);

            Assert.StartsWith("telemetry --event", command);
        }

        [TestMethod]
        public void SendTelemetryCommand_EscapesDoubleQuotes()
        {
            var jsonEvent = "{\"event\":\"test\"}";

            var command = _commandProvider.SendTelemetryCommand(jsonEvent);

            Assert.AreEqual("telemetry --event \"{\\\"event\\\":\\\"test\\\"}\"", command);
        }

        [TestMethod]
        public void SendTelemetryCommand_EmptyJson_ReturnsCommandWithEmptyJson()
        {
            var jsonEvent = "{}";

            var command = _commandProvider.SendTelemetryCommand(jsonEvent);

            Assert.AreEqual("telemetry --event \"{}\"", command);
        }

        [TestMethod]
        public void SendTelemetryCommand_ComplexJson_EscapesAllQuotes()
        {
            var jsonEvent = "{\"name\":\"test\",\"value\":\"data\"}";

            var command = _commandProvider.SendTelemetryCommand(jsonEvent);

            Assert.AreEqual("telemetry --event \"{\\\"name\\\":\\\"test\\\",\\\"value\\\":\\\"data\\\"}\"", command);
        }

        private static FnToRefactorModel CreateFnToRefactor(string name = "TestFunction", string nippyB64 = null)
        {
            return new FnToRefactorModel
            {
                Name = name,
                Body = "function body",
                FileType = "cs",
                NippyB64 = nippyB64,
            };
        }

        [TestMethod]
        public void GetRefactorPostCommand_WithoutSkipCache_ReturnsBasicCommand()
        {
            var fnToRefactor = CreateFnToRefactor();

            var command = _commandProvider.GetRefactorPostCommand(fnToRefactor, skipCache: false);

            Assert.StartsWith("refactor post", command);
            Assert.DoesNotContain("--skip-cache", command);
        }

        [TestMethod]
        public void GetRefactorPostCommand_WithSkipCache_IncludesSkipCacheFlag()
        {
            var fnToRefactor = CreateFnToRefactor();

            var command = _commandProvider.GetRefactorPostCommand(fnToRefactor, skipCache: true);

            Assert.Contains("--skip-cache", command);
        }

        [TestMethod]
        public void GetRefactorPostCommand_WithToken_IncludesTokenArgument()
        {
            var fnToRefactor = CreateFnToRefactor();
            var token = "test-token-123";

            var command = _commandProvider.GetRefactorPostCommand(fnToRefactor, skipCache: false, token: token);

            Assert.Contains("--token", command);
            Assert.Contains(token, command);
        }

        [TestMethod]
        public void GetRefactorPostCommand_WithNullToken_DoesNotIncludeTokenArgument()
        {
            var fnToRefactor = CreateFnToRefactor();

            var command = _commandProvider.GetRefactorPostCommand(fnToRefactor, skipCache: false, token: null);

            Assert.DoesNotContain("--token", command);
        }

        [TestMethod]
        public void GetRefactorPostCommand_WithEmptyToken_DoesNotIncludeTokenArgument()
        {
            var fnToRefactor = CreateFnToRefactor();

            var command = _commandProvider.GetRefactorPostCommand(fnToRefactor, skipCache: false, token: string.Empty);

            Assert.DoesNotContain("--token", command);
        }

        [TestMethod]
        public void GetRefactorPostCommand_WithWhitespaceToken_DoesNotIncludeTokenArgument()
        {
            var fnToRefactor = CreateFnToRefactor();

            var command = _commandProvider.GetRefactorPostCommand(fnToRefactor, skipCache: false, token: "   ");

            Assert.DoesNotContain("--token", command);
        }

        [TestMethod]
        public void GetRefactorPostCommand_WithNippyB64_UsesFnToRefactorNippyB64Flag()
        {
            var nippyB64 = "base64encodeddata";
            var fnToRefactor = CreateFnToRefactor(nippyB64: nippyB64);

            var command = _commandProvider.GetRefactorPostCommand(fnToRefactor, skipCache: false);

            Assert.Contains("--fn-to-refactor-nippy-b64", command);
            Assert.Contains(nippyB64, command);
            Assert.DoesNotContain("--fn-to-refactor ", command);
        }

        [TestMethod]
        public void GetRefactorPostCommand_WithoutNippyB64_UsesFnToRefactorJsonFlag()
        {
            var fnToRefactor = CreateFnToRefactor(nippyB64: null);

            var command = _commandProvider.GetRefactorPostCommand(fnToRefactor, skipCache: false);

            Assert.Contains("--fn-to-refactor", command);
            Assert.DoesNotContain("--fn-to-refactor-nippy-b64", command);
        }

        [TestMethod]
        public void GetRefactorPostCommand_WithEmptyNippyB64_UsesFnToRefactorJsonFlag()
        {
            var fnToRefactor = CreateFnToRefactor(nippyB64: string.Empty);

            var command = _commandProvider.GetRefactorPostCommand(fnToRefactor, skipCache: false);

            Assert.Contains("--fn-to-refactor", command);
            Assert.DoesNotContain("--fn-to-refactor-nippy-b64", command);
        }

        [TestMethod]
        public void GetRefactorPostCommand_WithAllOptions_IncludesAllArguments()
        {
            var nippyB64 = "encodeddata";
            var fnToRefactor = CreateFnToRefactor(nippyB64: nippyB64);
            var token = "my-token";

            var command = _commandProvider.GetRefactorPostCommand(fnToRefactor, skipCache: true, token: token);

            Assert.Contains("refactor", command);
            Assert.Contains("post", command);
            Assert.Contains("--skip-cache", command);
            Assert.Contains("--token", command);
            Assert.Contains(token, command);
            Assert.Contains("--fn-to-refactor-nippy-b64", command);
            Assert.Contains(nippyB64, command);
        }

        [TestMethod]
        public void GetRefactorPostCommand_JsonSerializesFnToRefactor_WhenNoNippyB64()
        {
            var fnToRefactor = new FnToRefactorModel
            {
                Name = "MyFunction",
                Body = "code here",
                FileType = "js",
                NippyB64 = null,
            };

            var command = _commandProvider.GetRefactorPostCommand(fnToRefactor, skipCache: false);

            // The command should contain serialized JSON with the function name
            Assert.Contains("MyFunction", command);
        }

        [TestMethod]
        [DataRow(null, DisplayName = "null cache path")]
        [DataRow("", DisplayName = "empty cache path")]
        [DataRow("   ", DisplayName = "whitespace cache path")]
        public void GetReviewFileContentPayload_WithInvalidCachePath_OmitsCachePath(string cachePath)
        {
            var payload = _commandProvider.GetReviewFileContentPayload(TestFileName, TestFileContent, cachePath);

            Assert.AreEqual($"{{\"path\":\"{TestFileName}\",\"file-content\":\"{TestFileContent}\"}}", payload);
        }

        [TestMethod]
        [DataRow(null, DisplayName = "null cache path")]
        [DataRow("", DisplayName = "empty cache path")]
        [DataRow("   ", DisplayName = "whitespace cache path")]
        public void GetRefactorWithCodeSmellsPayload_WithInvalidCachePath_OmitsCachePathFromJson(string cachePath)
        {
            var codeSmells = CreateTestCodeSmells();

            var content = _commandProvider.GetRefactorWithCodeSmellsPayload(TestFileName, TestFileContent, cachePath, codeSmells, null);

            Assert.AreEqual($"{{\"code-smells\":[{{\"category\":\"{codeSmells.First().Category}\"}}],\"file-name\":\"{TestFileName}\",\"file-content\":\"{TestFileContent}\"}}", content);
            Assert.DoesNotContain("cache-path", content);
        }

        [TestMethod]
        [DataRow(null, DisplayName = "null cache path")]
        [DataRow("", DisplayName = "empty cache path")]
        [DataRow("   ", DisplayName = "whitespace cache path")]
        public void GetRefactorWithDeltaResultPayload_WithInvalidCachePath_OmitsCachePathFromJson(string cachePath)
        {
            var deltaResult = CreateTestDeltaResult();

            var content = _commandProvider.GetRefactorWithDeltaResultPayload(TestFileName, TestFileContent, cachePath, deltaResult, null);

            var expectedNewScore = deltaResult.NewScore.ToString("0.0", CultureInfo.InvariantCulture);
            var expectedOldScore = deltaResult.OldScore.ToString("0.0", CultureInfo.InvariantCulture);
            Assert.AreEqual($"{{\"delta-result\":{{\"new-score\":{expectedNewScore},\"old-score\":{expectedOldScore}}},\"file-name\":\"{TestFileName}\",\"file-content\":\"{TestFileContent}\"}}", content);
            Assert.DoesNotContain("cache-path", content);
        }
    }
}
