// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Ace;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ContractInboundTests
    {
        private static readonly JsonSerializer Production = JsonSerializer.CreateDefault();

        private static readonly JsonSerializer Webview = JsonSerializer.Create(new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
        });

        private static ContractBundle _bundle = null!;

        private readonly List<ContractClientHarness> _harnesses = new List<ContractClientHarness>();

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _bundle = ContractBundle.Locate();
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var harness in _harnesses)
            {
                harness.Dispose();
            }
        }

        [TestMethod]
        [DataRow("review", new[] { "full", "minimal" })]
        [DataRow("delta", new[] { "full", "minimal" })]
        [DataRow("file-review-notification", new[] { "full", "minimal" })]
        [DataRow("delta-review-notification", new[] { "full", "minimal", "null-result" })]
        [DataRow("review-failed-notification", new[] { "full", "minimal" })]
        [DataRow("start-notification", new[] { "full", "minimal" })]
        public void ReplayedVariants_AreTheVariantsTheBundleShips(string root, string[] replayed)
        {
            CollectionAssert.AreEquivalent(replayed, _bundle.FixtureVariants(root).ToArray());
        }

        [TestMethod]
        [DataRow("minimal")]
        [DataRow("full")]
        public void ReviewResult_ThroughNormalizerAndModel_KeepsTheFixtureFields(string variant)
        {
            var wire = WireFixture("review", variant);

            var model = RpcJsonNormalizer.ToKebabCase(wire).ToObject<CliReviewModel>(Production);

            AssertEntity("review", variant, JObject.FromObject(model, Production));
        }

        [TestMethod]
        [DataRow("minimal")]
        [DataRow("full")]
        public void DeltaResult_ThroughNormalizerAndModel_KeepsTheFixtureFields(string variant)
        {
            var wire = WireFixture("delta", variant);

            var model = RpcJsonNormalizer.ToKebabCase(wire).ToObject<DeltaResponseModel>(Production);

            AssertEntity("delta", variant, JObject.FromObject(model, Production));
        }

        [TestMethod]
        [DataRow("minimal")]
        [DataRow("full")]
        public void DeltaResult_ThroughCodeHealthMonitorMapper_KeepsTheFixtureFields(string variant)
        {
            var wire = WireFixture("delta", variant);
            var model = RpcJsonNormalizer.ToKebabCase(wire).ToObject<DeltaResponseModel>(Production);

            var entity = MappedDeltaEntity(model);

            AssertEntity("delta", variant, entity);
        }

        [TestMethod]
        [DataRow("minimal")]
        [DataRow("full")]
        public async Task FileReviewNotification_OverTheClient_KeepsTheFixtureFields(string variant)
        {
            var wire = WireFixture("file-review-notification", variant);
            var harness = await StartHarnessAsync();
            var received = new TaskCompletionSource<ReviewNotification>();
            harness.Client.ReviewReceived += (_, notification) => received.TrySetResult(notification);

            await harness.ServerRpc.NotifyWithParameterObjectAsync("cs-ide/fileReview", wire);
            var notification = await WaitForAsync(received.Task);

            AssertEntity("file-review-notification", variant, JObject.FromObject(notification, Production));
        }

        [TestMethod]
        [DataRow("minimal")]
        [DataRow("full")]
        [DataRow("null-result")]
        public async Task DeltaReviewNotification_OverTheClient_KeepsTheFixtureFields(string variant)
        {
            var wire = WireFixture("delta-review-notification", variant);
            var harness = await StartHarnessAsync();
            var received = new TaskCompletionSource<DeltaNotification>();
            harness.Client.DeltaReceived += (_, notification) => received.TrySetResult(notification);

            await harness.ServerRpc.NotifyWithParameterObjectAsync("cs-ide/deltaReview", wire);
            var notification = await WaitForAsync(received.Task);

            AssertEntity("delta-review-notification", variant, JObject.FromObject(notification, Production));
        }

        [TestMethod]
        [DataRow("minimal")]
        [DataRow("full")]
        public async Task DeltaReviewNotification_ThroughCodeHealthMonitorMapper_KeepsTheFixtureFields(string variant)
        {
            var wire = WireFixture("delta-review-notification", variant);
            var harness = await StartHarnessAsync();
            var received = new TaskCompletionSource<DeltaNotification>();
            harness.Client.DeltaReceived += (_, notification) => received.TrySetResult(notification);

            await harness.ServerRpc.NotifyWithParameterObjectAsync("cs-ide/deltaReview", wire);
            var notification = await WaitForAsync(received.Task);

            var fixture = (JObject)_bundle.Fixture("delta-review-notification", variant, FixtureForm.Entity);
            var expected = (JObject)(fixture.GetValue("result") ?? throw new InvalidOperationException("The fixture has no result")).DeepClone();
            expected.Remove("old-git-blob-sha");
            expected.Remove("new-git-blob-sha");
            AssertAgainst(expected, "delta.json", "Delta", MappedDeltaEntity(notification.Result));
        }

        [TestMethod]
        [DataRow("minimal")]
        [DataRow("full")]
        public async Task ReviewFailedNotification_OverTheClient_KeepsTheFixtureFields(string variant)
        {
            var wire = WireFixture("review-failed-notification", variant);
            var harness = await StartHarnessAsync();
            var received = new TaskCompletionSource<ReviewFailedNotification>();
            harness.Client.ReviewFailed += (_, notification) => received.TrySetResult(notification);

            await harness.ServerRpc.NotifyWithParameterObjectAsync("cs-ide/reviewFailed", wire);
            var notification = await WaitForAsync(received.Task);

            AssertEntity("review-failed-notification", variant, JObject.FromObject(notification, Production));
        }

        [TestMethod]
        [DataRow("minimal")]
        [DataRow("full")]
        public async Task StartNotification_OverTheClient_KeepsTheFixtureFields(string variant)
        {
            var wire = WireFixture("start-notification", variant);
            var harness = await StartHarnessAsync(wire);

            var metadata = await harness.Client.StartAsync();

            AssertEntity("start-notification", variant, JObject.FromObject(metadata, Production));
        }

        [TestMethod]
        public async Task StartNotification_WithoutCapabilities_YieldsNoCapabilities()
        {
            var wire = (JObject)WireFixture("start-notification", "minimal").DeepClone();
            wire.Remove("capabilities");
            var harness = await StartHarnessAsync(wire);

            var metadata = await harness.Client.StartAsync();

            Assert.IsEmpty(metadata.Capabilities);
        }

        [TestMethod]
        public void DeltaWithoutScores_ThroughCodeHealthMonitorMapper_LeavesTheScoresAbsent()
        {
            var wire = WireFixture("delta", "minimal");
            var model = RpcJsonNormalizer.ToKebabCase(wire).ToObject<DeltaResponseModel>(Production);

            var entity = (JObject)MappedDeltaEntity(model);

            Assert.IsFalse(entity.ContainsKey("old-score"));
            Assert.IsFalse(entity.ContainsKey("new-score"));
            Assert.AreEqual(-1.5m, entity.Value<decimal>("score-change"));
        }

        [TestMethod]
        public void DeltaWithZeroScores_ThroughCodeHealthMonitorMapper_KeepsTheZeros()
        {
            var model = new DeltaResponseModel { OldScore = 0m, NewScore = 0m, ScoreChange = 0m };

            var entity = (JObject)MappedDeltaEntity(model);

            Assert.AreEqual(0m, entity.Value<decimal>("old-score"));
            Assert.AreEqual(0m, entity.Value<decimal>("new-score"));
        }

        [TestMethod]
        public void Comparison_WithAnOptionalScoreDefaultedToZero_FailsNamingTheField()
        {
            var wire = WireFixture("delta", "minimal");
            var model = RpcJsonNormalizer.ToKebabCase(wire).ToObject<DeltaResponseModel>(Production);
            model.OldScore = 0m;

            var failure = Assert.Throws<AssertFailedException>(() => AssertEntity("delta", "minimal", JObject.FromObject(model, Production)));

            StringAssert.Contains(failure.Message, "$.old-score: fixture has (absent), entity has 0");
        }

        [TestMethod]
        public void Comparison_WithADroppedFixtureField_FailsNamingTheField()
        {
            var wire = WireFixture("review", "full");
            var model = RpcJsonNormalizer.ToKebabCase(wire).ToObject<CliReviewModel>(Production);
            model.RawScore = null;

            var failure = Assert.Throws<AssertFailedException>(() => AssertEntity("review", "full", JObject.FromObject(model, Production)));

            StringAssert.Contains(failure.Message, "raw-score");
        }

        [TestMethod]
        public void FieldPaths_ListsEveryLeafOfBothTokensAcrossArrays()
        {
            var first = JObject.Parse(@"{""a"":1,""list"":[{""b"":2},{""c"":3}],""scalars"":[""x""],""empty"":[],""nested"":{""d"":null}}");
            var second = JObject.Parse(@"{""e"":5}");

            var paths = ContractEntityForm.FieldPaths(first, second);

            CollectionAssert.AreEqual(new[] { "a", "e", "empty", "list.b", "list.c", "nested.d", "scalars" }, paths.ToArray());
        }

        [TestMethod]
        public void WithoutNullsAbsentFrom_DropsOnlyNullsTheFixtureDoesNotHave()
        {
            var fixture = JObject.Parse(@"{""kept"":null,""items"":[{""inner"":null}]}");
            var entity = JObject.Parse(@"{""kept"":null,""dropped"":null,""value"":0,""items"":[{""inner"":null,""extra"":null},{""extra"":null}]}");

            var result = ContractEntityForm.WithoutNullsAbsentFrom(entity, fixture);

            Assert.AreEqual(@"{""kept"":null,""value"":0,""items"":[{""inner"":null},{}]}", result.ToString(Formatting.None));
        }

        private static JObject WireFixture(string root, string variant)
        {
            var wire = (JObject)_bundle.Fixture(root, variant, FixtureForm.Wire);
            ContractBundle.AssertCasing(wire, Casing.Camel);
            return wire;
        }

        private static JToken MappedDeltaEntity(DeltaResponseModel model)
        {
            var preflight = new Mock<IPreflightManager>();
            preflight.Setup(x => x.GetAutoRefactorConfig()).Returns(new AutoRefactorConfig { Activated = true, Visible = true, Disabled = false });
            var mapper = new CodeHealthMonitorMapper(preflight.Object);

            var data = mapper.Map(new Dictionary<string, DeltaResponseModel> { { "src/fake.js", model } });

            return ContractBundle.ToKebabKeys(JObject.FromObject(data.FileDeltaData[0].Delta, Webview));
        }

        private static void AssertEntity(string root, string variant, JToken actual)
        {
            var (schemaFile, definition) = _bundle.FixtureSchema(root);
            AssertAgainst(_bundle.Fixture(root, variant, FixtureForm.Entity), schemaFile, definition, actual);
        }

        private static void AssertAgainst(JToken fixture, string schemaFile, string definition, JToken actual)
        {
            var entity = ContractEntityForm.WithoutNullsAbsentFrom(actual, fixture);
            ContractBundle.AssertCasing(entity, Casing.Kebab);
            ContractBundle.AssertDeclaredFieldsMatch(fixture, entity, ContractEntityForm.FieldPaths(fixture, entity));
            _bundle.Validate(schemaFile, definition, entity);
        }

        private static async Task<T> WaitForAsync<T>(Task<T> task)
        {
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
            if (completed != task)
            {
                throw new TimeoutException("Timed out waiting for the notification to reach the client.");
            }

            return await task;
        }

        private async Task<ContractClientHarness> StartHarnessAsync(JObject? startWire = null)
        {
            var harness = ContractClientHarness.Create(startWire ?? WireFixture("start-notification", "minimal"));
            _harnesses.Add(harness);
            await harness.Client.StartAsync();
            return harness;
        }
    }
}
