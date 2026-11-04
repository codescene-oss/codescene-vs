// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Ace;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Application.Services;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Message;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.Core.Models.WebComponent.Payload;
using Codescene.VSExtension.Core.Util;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using static Codescene.VSExtension.Core.Consts.WebComponentConstants;

namespace Codescene.VSExtension.Core.Tests
{
    /// <summary>
    /// The webview boundary (D11, R12): what the extension sends to the webview validates against
    /// <c>webview-schema.json</c> and what the webview sends to the extension against <c>message-schema.json</c>,
    /// both from the CWF release the extension bundles (<c>cs-cwf/</c>). The schemas are closed, so every payload
    /// is also checked to fail once it carries a field the webview does not declare.
    ///
    /// The payloads come from the production builders (<see cref="CodeHealthMonitorMapper"/>,
    /// <see cref="CodeSmellDocumentationMapper"/>, <see cref="WebComponentPayload{T}"/>) fed with the delta and
    /// review fixtures of the contract bundle, serialized with the settings the tool windows use
    /// (<see cref="WebComponentJson"/>). The messages are samples of what the webview emits, read the way
    /// <c>WebComponentMessageHandler</c> reads them. ACE views and ACE messages are not exercised.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class ContractWebviewTests
    {
        private const string FilePath = "src/fake.js";

        private static readonly JsonSerializer Production = JsonSerializer.CreateDefault();

        private static readonly JsonSerializer WebviewReads = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            NullValueHandling = NullValueHandling.Ignore,
        });

        private static ContractBundle _bundle = null!;
        private static CwfSchemas _cwf = null!;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
            _cwf = CwfSchemas.Locate();
        }

        [TestInitialize]
        public void Initialize()
        {
            DeltaJobTracker.Clear();
        }

        [TestCleanup]
        public void Cleanup()
        {
            DeltaJobTracker.Clear();
        }

        [TestMethod]
        [DataRow(CwfSchemas.WebviewSchemaFile)]
        [DataRow(CwfSchemas.MessageSchemaFile)]
        public void Schemas_WhenTheFileIsMissing_FailInsteadOfSkipping(string schemaFile)
        {
            var empty = Path.Combine(Path.GetTempPath(), "cwf-missing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(empty);
            try
            {
                var schemas = CwfSchemas.At(empty);

                var failure = Assert.Throws<InvalidOperationException>(() =>
                {
                    if (schemaFile == CwfSchemas.WebviewSchemaFile)
                    {
                        schemas.ValidatePayload(new JObject());
                    }
                    else
                    {
                        schemas.ValidateMessage(new JObject());
                    }
                });

                StringAssert.Contains(failure.Message, schemaFile + " is missing");
                StringAssert.Contains(failure.Message, "cwf.ps1");
            }
            finally
            {
                Directory.Delete(empty, true);
            }
        }

        [TestMethod]
        public void PayloadSchema_IsClosedAndRootedAtTheViews()
        {
            _cwf.ValidatePayload(JObject.Parse(@"{""ideType"":""Visual Studio"",""view"":""auth""}"));

            AssertFails(() => _cwf.ValidatePayload(JObject.Parse(@"{""ideType"":""Visual Studio"",""view"":""auth"",""undeclared"":1}")), "undeclared");
            AssertFails(() => _cwf.ValidatePayload(JObject.Parse(@"{""ideType"":""Visual Studio"",""view"":""nonexistent""}")), "view");
        }

        [TestMethod]
        public void MessageSchema_IsClosedAndRootedAtTheMessageTypes()
        {
            _cwf.ValidateMessage(JObject.Parse(@"{""messageType"":""open-settings""}"));

            AssertFails(() => _cwf.ValidateMessage(JObject.Parse(@"{""messageType"":""open-settings"",""undeclared"":1}")), "undeclared");
            AssertFails(() => _cwf.ValidateMessage(JObject.Parse(@"{""messageType"":""nonexistent""}")), "messageType");
        }

        [TestMethod]
        public void AsTheWebviewReadsIt_ConvertsOnlyTheDeltaOfEachFileToKebabCase()
        {
            var sent = JObject.Parse(@"{""view"":""home"",""featureFlags"":[""ide-auth""],""data"":{""fileDeltaData"":[{""file"":{""fileName"":""a.js""},""delta"":{""scoreChange"":1,""functionLevelFindings"":[{""refactorableFn"":{""nippyB64"":""x""}}]}}]}}");

            var read = CwfSchemas.AsTheWebviewReadsIt(sent);

            Assert.AreEqual(
                @"{""view"":""home"",""featureFlags"":[""ide-auth""],""data"":{""fileDeltaData"":[{""file"":{""fileName"":""a.js""},""delta"":{""score-change"":1,""function-level-findings"":[{""refactorable-fn"":{""nippy-b64"":""x""}}]}}]}}",
                read.ToString(Formatting.None));
            Assert.AreEqual(1, (int)At(sent, "data.fileDeltaData[0].delta.scoreChange"), "the input is not modified");
        }

        [TestMethod]
        [DataRow("full")]
        [DataRow("minimal")]
        public void HomePayload_FromTheDeltaFixture_ValidatesAndIsClosed(string variant)
        {
            var payload = HomePayload(Deltas(DeltaModel("delta", variant)));

            Assert.AreEqual(1, ((JArray)At(payload, "data.fileDeltaData")).Count);
            AssertValidAndClosed(payload, DeltaOf);
        }

        [TestMethod]
        [DataRow("full")]
        [DataRow("minimal")]
        public void HomePayload_FromTheDeltaReviewNotificationFixture_ValidatesAndIsClosed(string variant)
        {
            var wire = (JObject)_bundle.Fixture("delta-review-notification", variant, FixtureForm.Wire);
            var model = Read<DeltaResponseModel>(RpcJsonNormalizer.ToKebabCase(At(wire, "result")));

            var payload = HomePayload(Deltas(model));

            AssertValidAndClosed(payload, DeltaOf);
        }

        [TestMethod]
        public void HomePayload_ForTheMinimalDelta_HasExactlyTheRequiredDeltaFields()
        {
            var delta = DeltaOf(HomePayload(Deltas(DeltaModel("delta", "minimal"))));

            CollectionAssert.AreEquivalent(
                new[] { "scoreChange", "fileLevelFindings", "functionLevelFindings" },
                delta.Properties().Select(property => property.Name).ToArray());
        }

        [TestMethod]
        public void HomePayload_WithoutAnyDelta_ValidatesAndIsClosed()
        {
            var payload = HomePayload(Deltas());

            Assert.AreEqual(0, ((JArray)At(payload, "data.fileDeltaData")).Count);
            AssertValidAndClosed(payload, copy => (JObject)At(copy, "data"));
        }

        [TestMethod]
        public void HomePayload_WithAFileLevelFindingWithoutALine_ValidatesAndIsClosed()
        {
            var model = DeltaModel("delta", "full");
            model.FileLevelFindings[0].Line = null;

            var payload = HomePayload(Deltas(model));

            Assert.IsFalse(((JObject)At(DeltaOf(payload), "fileLevelFindings[0]")).ContainsKey("line"));
            AssertValidAndClosed(payload, DeltaOf);
        }

        [TestMethod]
        public void HomePayload_WhileTheAnalysisRuns_ValidatesAndIsClosed()
        {
            var notifier = new CodeHealthMonitorNotifier();
            notifier.ApplyQueue(new ReviewQueue { Count = 2, Done = 1, Files = new[] { FilePath, "src/other.js" } });

            var payload = HomePayload(Deltas(DeltaModel("delta", "full")), notifier: notifier);

            var data = At(payload, "data");
            Assert.AreEqual("running", (string?)data["analysisState"]);
            CollectionAssert.AreEqual(new[] { "running", "queued" }, ((JArray)At(data, "jobs")).Select(job => (string?)job["state"]).ToArray());
            AssertValidAndClosed(payload, copy => (JObject)At(copy, "data.jobs[0]"));
            AssertValidAndClosed(payload, copy => (JObject)At(copy, "data.autoRefactor"));
        }

        [TestMethod]
        public void HomePayload_WhenTheAnalysisIsStopped_ValidatesAndIsClosed()
        {
            var notifier = new CodeHealthMonitorNotifier();
            notifier.SetAutomaticAnalysisEnabled(false);

            var payload = HomePayload(Deltas(DeltaModel("delta", "full")), notifier: notifier);

            Assert.AreEqual("stopped", (string?)At(payload, "data.analysisState"));
            AssertValidAndClosed(payload, DeltaOf);
        }

        [TestMethod]
        public async Task HomePayload_WithAceEnabled_ValidatesAndIsClosed()
        {
            var preflight = await PreflightManagerWithAceEnabledAsync();

            var payload = HomePayload(Deltas(DeltaModel("delta", "full")), preflight);

            Assert.AreEqual("enabled", (string?)At(payload, "data.autoRefactor.aceStatus.status"));
            AssertValidAndClosed(payload, copy => (JObject)At(copy, "data.autoRefactor.aceStatus"));
        }

        [TestMethod]
        public void UpdateRendererMessage_CarriesAPayloadThatValidatesAndIsClosed()
        {
            var mapper = new CodeHealthMonitorMapper(RealPreflightManager(), new CodeHealthMonitorNotifier());
            var message = new WebComponentMessage<CodeHealthMonitorComponentData>
            {
                MessageType = MessageTypes.UPDATERENDERER,
                Payload = WebComponentPayload<CodeHealthMonitorComponentData>.Create(ViewTypes.HOME, mapper.Map(Deltas(DeltaModel("delta", "full"))), true),
            };

            var sent = Sent(message);

            Assert.AreEqual("update-renderer", (string?)sent["messageType"]);
            AssertValidAndClosed((JObject)At(sent, "payload"), DeltaOf);
        }

        [TestMethod]
        public void HomePayload_SendsOnlyFeatureFlagsTheWebviewDeclares()
        {
            var payload = HomePayload(Deltas());

            var flags = ((JArray)At(payload, "featureFlags")).Select(flag => (string?)flag).ToArray();
            CollectionAssert.DoesNotContain(flags, "jobs");
            CollectionAssert.DoesNotContain(flags, "open-settings");
            CollectionAssert.Contains(flags, "ace-status-indicator");
        }

        [TestMethod]
        public void FunctionFindingModel_WithARefactorableFn_UsesTheKeyTheWebviewDeclares()
        {
            var finding = new FunctionFindingModel
            {
                Function = new FunctionInfoModel { Name = "fakeFunction" },
                ChangeDetails = Array.Empty<ChangeDetailModel>(),
                RefactorableFn = new FnToRefactorModel { Name = "fakeFunction", Body = "function fakeFunction() {}", FileType = "js" },
            };

            var json = JObject.FromObject(finding, Production);

            ContractBundle.AssertCasing(json, Casing.Kebab);
            var declared = _cwf.DeclaredProperties("FunctionFinding");
            var undeclared = json.Properties().Select(property => property.Name).Where(name => !declared.Contains(name)).ToList();
            Assert.IsEmpty(undeclared, "keys the webview does not declare for a function finding: " + string.Join(", ", undeclared));
            Assert.IsNotNull(json["refactorable-fn"]);
        }

        [TestMethod]
        public void FunctionFindingModel_ReadingTheWebviewKey_KeepsTheRefactorableFn()
        {
            var json = JObject.Parse(@"{""function"":{""name"":""fakeFunction""},""change-details"":[],""refactorable-fn"":{""name"":""fakeFunction"",""body"":""b"",""file-type"":""js""}}");

            var finding = Read<FunctionFindingModel>(json);

            Assert.AreEqual("fakeFunction", finding.RefactorableFn.Name);
            Assert.AreEqual("js", finding.RefactorableFn.FileType);
        }

        [TestMethod]
        [DataRow(true, true)]
        [DataRow(true, false)]
        [DataRow(false, false)]
        public void DocsPayload_ForAFunctionLevelSmell_ValidatesAndIsClosed(bool hasToken, bool aceAcknowledged)
        {
            var smell = (JObject)At(_bundle.Fixture("review", "full", FixtureForm.Entity), "function-level-code-smells[0]");
            var model = new ShowDocumentationModel(
                FilePath,
                (string)At(smell, "code-smells[0].category"),
                (string)At(smell, "function"),
                RangeOf(At(smell, "range")));

            var payload = DocsPayload(model, hasToken, aceAcknowledged);

            Assert.AreEqual("docs_issues_complex_method", (string?)At(payload, "data.docType"));
            AssertValidAndClosed(payload, copy => (JObject)At(copy, "data.fileData"));
            AssertValidAndClosed(payload, copy => (JObject)At(copy, "data.fileData.fn"));
            AssertValidAndClosed(payload, copy => (JObject)At(copy, "data.autoRefactor"));
        }

        [TestMethod]
        public void DocsPayload_ForAFileLevelSmell_ValidatesAndIsClosed()
        {
            var smell = (JObject)At(_bundle.Fixture("review", "full", FixtureForm.Entity), "file-level-code-smells[0]");
            var model = new ShowDocumentationModel(FilePath, (string)At(smell, "category"), null!, null!);

            var payload = DocsPayload(model, hasToken: true, aceAcknowledged: true);

            Assert.AreEqual("docs_issues_lines_of_code_in_a_single_file", (string?)At(payload, "data.docType"));
            AssertValidAndClosed(payload, copy => (JObject)At(copy, "data.fileData"));
        }

        [TestMethod]
        public void DocsPayload_OpenedFromTheMonitor_ValidatesAndIsClosed()
        {
            var model = new ShowDocumentationModel(FilePath, "docs_issues_complex_method", "fakeFunction", new CodeRangeModel(3, 9, 1, 2));

            var payload = DocsPayload(model, hasToken: true, aceAcknowledged: true);

            AssertValidAndClosed(payload, copy => (JObject)At(copy, "data.fileData.fn.range"));
        }

        [TestMethod]
        public void DocsUpdateRendererMessage_CarriesAPayloadThatValidatesAndIsClosed()
        {
            var model = new ShowDocumentationModel(FilePath, "Complex Method", "fakeFunction", new CodeRangeModel(3, 9, 1, 2));
            var message = new WebComponentMessage<CodeSmellDocumentationComponentData>
            {
                MessageType = MessageTypes.UPDATERENDERER,
                Payload = WebComponentPayload<CodeSmellDocumentationComponentData>.Create(ViewTypes.DOCS, DocsMapper(hasToken: false).Map(model, null!)),
            };

            var sent = Sent(message);

            Assert.AreEqual("update-renderer", (string?)sent["messageType"]);
            AssertValidAndClosed((JObject)At(sent, "payload"), copy => (JObject)At(copy, "data.fileData"));
        }

        [TestMethod]
        [DataRow(@"{""messageType"":""init"",""payload"":""home""}", MessageTypes.INIT)]
        [DataRow(@"{""messageType"":""init"",""payload"":""docs""}", MessageTypes.INIT)]
        [DataRow(@"{""messageType"":""open-settings""}", MessageTypes.OPENSETTINGS)]
        [DataRow(@"{""messageType"":""showLogoutput""}", MessageTypes.SHOWLOGOUTPUT)]
        [DataRow(@"{""messageType"":""stop-analysis""}", MessageTypes.STOPANALYSIS)]
        [DataRow(@"{""messageType"":""start-analysis""}", MessageTypes.STARTANALYSIS)]
        [DataRow(@"{""messageType"":""goto-function-location"",""payload"":{""fileName"":""src/fake.js""}}", MessageTypes.GOTOFUNCTIONLOCATION)]
        [DataRow(@"{""messageType"":""goto-function-location"",""payload"":{""fileName"":""src/fake.js"",""fn"":{""name"":""fakeFunction"",""range"":{""startLine"":3,""startColumn"":1,""endLine"":9,""endColumn"":2}}}}", MessageTypes.GOTOFUNCTIONLOCATION)]
        [DataRow(@"{""messageType"":""open-docs-for-function"",""payload"":{""docType"":""docs_issues_complex_method"",""fileName"":""src/fake.js""}}", MessageTypes.OPENDOCSFORFUNCTION)]
        [DataRow(@"{""messageType"":""open-docs-for-function"",""payload"":{""docType"":""docs_issues_complex_method"",""fileName"":""src/fake.js"",""fn"":{""name"":""fakeFunction"",""range"":{""startLine"":3,""startColumn"":1,""endLine"":9,""endColumn"":2}}}}", MessageTypes.OPENDOCSFORFUNCTION)]
        public void WebviewMessage_ValidatesAndIsReadByTheHandlerWithoutLosingFields(string sample, string messageType)
        {
            var message = JObject.Parse(sample);
            _cwf.ValidateMessage(message);

            var read = ReadMessage(sample);

            Assert.AreEqual(messageType, read.MessageType);
            if (read.Payload is JObject)
            {
                var type = PayloadClass(messageType) ?? throw new AssertFailedException($"No payload class for {messageType}");
                var reserialized = JObject.FromObject(read.Payload.ToObject(type) ?? throw new AssertFailedException("The payload is empty"), WebviewReads);
                ContractBundle.AssertDeclaredFieldsMatch(At(message, "payload"), reserialized, ContractEntityForm.FieldPaths(reserialized));
            }
            else
            {
                Assert.AreEqual(message["payload"]?.ToString(), read.Payload?.ToString());
            }

            AssertMessageClosed(message);
        }

        [TestMethod]
        public void WebviewMessage_ForGotoFunctionLocation_IsReadWithTheFunctionRangeTheHandlerNavigatesTo()
        {
            var sample = @"{""messageType"":""goto-function-location"",""payload"":{""fileName"":""src/fake.js"",""fn"":{""name"":""fakeFunction"",""range"":{""startLine"":3,""startColumn"":1,""endLine"":9,""endColumn"":2}}}}";

            var payload = ReadMessage(sample).Payload.ToObject<GotoFunctionLocationPayload>() ?? throw new AssertFailedException("The payload is empty");

            Assert.AreEqual("src/fake.js", payload.FileName);
            Assert.AreEqual(3, payload.Fn.Range.StartLine);
            Assert.AreEqual(9, payload.Fn.Range.EndLine);
        }

        [TestMethod]
        public void WebviewMessage_WithAnUndeclaredPayloadField_Fails()
        {
            var message = JObject.Parse(@"{""messageType"":""goto-function-location"",""payload"":{""fileName"":""src/fake.js"",""line"":3}}");

            AssertFails(() => _cwf.ValidateMessage(message), "line");
        }

        [TestMethod]
        public void MessageTypes_AreAllEitherSampledAceOrSentToTheWebview()
        {
            var sampled = new[] { MessageTypes.INIT, MessageTypes.OPENSETTINGS, MessageTypes.SHOWLOGOUTPUT, MessageTypes.STOPANALYSIS, MessageTypes.STARTANALYSIS, MessageTypes.GOTOFUNCTIONLOCATION, MessageTypes.OPENDOCSFORFUNCTION };
            var ace = new[] { MessageTypes.APPLY, MessageTypes.ACKNOWLEDGED, MessageTypes.REJECT, MessageTypes.RETRY, MessageTypes.CLOSE, MessageTypes.COPYCODE, MessageTypes.SHOWDIFF, MessageTypes.REQUESTANDPRESENTREFACTORING, MessageTypes.CANCEL };
            var sentToTheWebview = new[] { MessageTypes.UPDATERENDERER };

            var all = typeof(MessageTypes).GetFields().Where(field => field.IsLiteral).Select(field => (string)(field.GetRawConstantValue() ?? string.Empty)).ToArray();

            CollectionAssert.AreEquivalent(all, sampled.Concat(ace).Concat(sentToTheWebview).ToArray(), "a new message type must be sampled here, or listed as ACE");
        }

        private static JToken At(JToken root, string path)
        {
            return root.SelectToken(path) ?? throw new InvalidOperationException($"{path} is missing in {root.ToString(Formatting.None)}");
        }

        private static T Read<T>(JToken token)
        {
            return token.ToObject<T>(Production) ?? throw new InvalidOperationException($"{token.ToString(Formatting.None)} is not a {typeof(T).Name}");
        }

        private static MessageObj<JToken> ReadMessage(string json)
        {
            return JsonConvert.DeserializeObject<MessageObj<JToken>>(json) ?? throw new InvalidOperationException("The message is empty");
        }

        private static Type? PayloadClass(string messageType)
        {
            switch (messageType)
            {
                case MessageTypes.GOTOFUNCTIONLOCATION:
                    return typeof(GotoFunctionLocationPayload);
                case MessageTypes.OPENDOCSFORFUNCTION:
                    return typeof(OpenDocsForFunctionPayload);
                default:
                    return null;
            }
        }

        private static JObject Sent(object payload)
        {
            return JObject.Parse(WebComponentJson.Serialize(payload));
        }

        private static IPreflightManager RealPreflightManager()
        {
            return new PreflightManager(
                new Mock<ICliExecutor>().Object,
                new Mock<ILogger>().Object,
                new Mock<IAceStateService>().Object,
                new Mock<ISettingsProvider>().Object);
        }

        private static async Task<IPreflightManager> PreflightManagerWithAceEnabledAsync()
        {
            var executor = new Mock<ICliExecutor>();
            executor.Setup(x => x.PreflightAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PreFlightResponseModel());
            var aceState = new Mock<IAceStateService>();
            aceState.Setup(x => x.CurrentState).Returns(Core.Enums.AceState.Enabled);
            var settings = new Mock<ISettingsProvider>();
            settings.Setup(x => x.AuthToken).Returns("valid-token");
            var preflight = new PreflightManager(executor.Object, new Mock<ILogger>().Object, aceState.Object, settings.Object);

            await preflight.RunPreflightAsync(true);

            return preflight;
        }

        private static CodeRangeModel RangeOf(JToken range)
        {
            return new CodeRangeModel((int)At(range, "start-line"), (int)At(range, "end-line"), (int)At(range, "start-column"), (int)At(range, "end-column"));
        }

        private static DeltaResponseModel DeltaModel(string root, string variant)
        {
            var wire = (JObject)_bundle.Fixture(root, variant, FixtureForm.Wire);
            return Read<DeltaResponseModel>(RpcJsonNormalizer.ToKebabCase(wire));
        }

        private static Dictionary<string, DeltaResponseModel> Deltas(params DeltaResponseModel[] models)
        {
            return models.ToDictionary(_ => FilePath);
        }

        private static JObject HomePayload(Dictionary<string, DeltaResponseModel> deltas, IPreflightManager? preflight = null, CodeHealthMonitorNotifier? notifier = null)
        {
            var mapper = new CodeHealthMonitorMapper(preflight ?? RealPreflightManager(), notifier ?? new CodeHealthMonitorNotifier());
            return Sent(WebComponentPayload<CodeHealthMonitorComponentData>.Create(ViewTypes.HOME, mapper.Map(deltas), true));
        }

        private static CodeSmellDocumentationMapper DocsMapper(bool hasToken)
        {
            var settings = new Mock<ISettingsProvider>();
            settings.Setup(x => x.AuthToken).Returns(hasToken ? "valid-token" : string.Empty);
            return new CodeSmellDocumentationMapper(settings.Object);
        }

        private static JObject DocsPayload(ShowDocumentationModel model, bool hasToken, bool aceAcknowledged)
        {
            return Sent(WebComponentPayload<CodeSmellDocumentationComponentData>.Create(ViewTypes.DOCS, DocsMapper(hasToken).Map(model, null!, aceAcknowledged)));
        }

        /// <summary>The first file's delta of a home payload (as sent, in camelCase).</summary>
        private static JObject DeltaOf(JObject payload)
        {
            return (JObject)At(payload, "data.fileDeltaData[0].delta");
        }

        private static void AssertFails(Action validate, string expectedInMessage)
        {
            var failure = Assert.Throws<AssertFailedException>(validate);
            StringAssert.Contains(failure.Message, expectedInMessage);
        }

        /// <summary>
        /// The payload, as the webview reads it, validates; and it fails once an undeclared field is added at the top
        /// level or to the nested object <paramref name="locate"/> finds in the payload as sent.
        /// </summary>
        private static void AssertValidAndClosed(JObject sent, Func<JObject, JObject> locate)
        {
            ContractBundle.AssertCasing(sent["data"] ?? new JObject(), Casing.Camel);
            _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(sent));

            var topLevel = (JObject)sent.DeepClone();
            topLevel["undeclared"] = true;
            AssertFails(() => _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(topLevel)), "undeclared");

            var nested = (JObject)sent.DeepClone();
            locate(nested)["undeclared"] = true;
            AssertFails(() => _cwf.ValidatePayload(CwfSchemas.AsTheWebviewReadsIt(nested)), "undeclared");
        }

        private static void AssertMessageClosed(JObject message)
        {
            var topLevel = (JObject)message.DeepClone();
            topLevel["undeclared"] = true;
            AssertFails(() => _cwf.ValidateMessage(topLevel), "undeclared");

            if (message["payload"] is JObject)
            {
                var nested = (JObject)message.DeepClone();
                ((JObject)At(nested, "payload"))["undeclared"] = true;
                AssertFails(() => _cwf.ValidateMessage(nested), "undeclared");
            }
        }
    }
}
