// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Models.Cli.Telemetry;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ContractOutboundTests
    {
        private static readonly JsonSerializer Production = JsonSerializer.CreateDefault();

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
        [DataRow("telemetry-params")]
        [DataRow("review-files-params")]
        [DataRow("watch-files-params")]
        [DataRow("stop-watch-files-params")]
        public void SentVariants_AreTheVariantsTheBundleShips(string root)
        {
            CollectionAssert.AreEquivalent(new[] { "full", "minimal" }, _bundle.FixtureVariants(root).ToArray());
        }

        [TestMethod]
        [DataRow("minimal")]
        [DataRow("full")]
        public async Task Telemetry_BuiltAndSentByTheClient_ValidatesWithKebabKeys(string variant)
        {
            var fixture = (JObject)_bundle.Fixture("telemetry-params", variant, FixtureForm.Wire);
            var fixtureEvent = (JObject)Required(fixture, "event");
            var (server, harness) = await StartHarnessAsync();
            var full = variant == "full";
            var additionalEventData = full
                ? new Dictionary<string, object>
                {
                    { "internal", true },
                    { "fake-extra-property", Text(fixtureEvent, "fake-extra-property") },
                    { "scoreChange", 1.5m },
                }
                : null;

            var eventJson = TelemetryUtils.GetTelemetryEventJson(
                "fake-event",
                (string?)fixtureEvent["user-id"],
                Text(fixtureEvent, "extension-version"),
                full ? "18.1.1" : null,
                additionalEventData);
            await harness.Client.TelemetryAsync(JsonConvert.DeserializeObject(eventJson) ?? throw new InvalidOperationException("The event was not built"));
            var sent = await WaitForAsync(server.Telemetry.Task);

            var expectedEvent = (JObject)fixtureEvent.DeepClone();
            expectedEvent["event-name"] = Constants.Telemetry.SOURCEIDE + "/fake-event";
            expectedEvent["editor-type"] = Constants.Telemetry.SOURCEIDE;
            if (full)
            {
                expectedEvent["editor-version"] = "18.1.1";
                expectedEvent["score-change"] = 1.5m;
            }

            AssertSent("telemetry-params", new JObject { ["event"] = expectedEvent }, sent);
        }

        [TestMethod]
        public async Task Telemetry_WithOptionalInputsMissing_SendsNoKeyForThem()
        {
            var (server, harness) = await StartHarnessAsync();

            var eventJson = TelemetryUtils.GetTelemetryEventJson("fake-event", null, "0.0.0-fake");
            await harness.Client.TelemetryAsync(JsonConvert.DeserializeObject(eventJson) ?? throw new InvalidOperationException("The event was not built"));
            var sent = await WaitForAsync(server.Telemetry.Task);

            var sentEvent = (JObject)Required(sent, "event");
            CollectionAssert.AreEquivalent(
                new[] { "event-name", "editor-type", "extension-version" },
                sentEvent.Properties().Select(property => property.Name).ToArray());
        }

        [TestMethod]
        public void TelemetryEvent_WithTheRequiredInputsMissing_KeepsTheirKeysSoTheRequiredCheckFails()
        {
            var telemetryEvent = new TelemetryEvent { ExtensionVersion = "0.0.0-fake" };

            var serialized = JObject.FromObject(telemetryEvent, Production);

            Assert.AreEqual(JTokenType.Null, serialized["event-name"]?.Type, "event-name must not be dropped when it is missing");
            Assert.AreEqual(JTokenType.Null, serialized["editor-type"]?.Type, "editor-type must not be dropped when it is missing");
            var failure = Assert.Throws<AssertFailedException>(() => _bundle.Validate("telemetry-event.json", "TelemetryEvent", serialized));
            StringAssert.Contains(failure.Message, "event-name");
            StringAssert.Contains(failure.Message, "editor-type");
        }

        [TestMethod]
        [DataRow("minimal")]
        [DataRow("full")]
        public async Task ReviewFiles_SentByTheClient_ValidatesWithKebabKeys(string variant)
        {
            var fixture = (JObject)_bundle.Fixture("review-files-params", variant, FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();
            var files = ((JArray)Required(fixture, "files")).Select(file => new ReviewFile
            {
                Id = (string?)file["id"],
                RelPath = Text(file, "rel-path"),
                Content = (string?)file["content"],
            }).ToList();

            harness.Client.ReviewFiles(Text(fixture, "repo-root"), files);
            var sent = await WaitForAsync(server.ReviewFiles.Task);

            AssertSent("review-files-params", WithoutBaselineRevision(fixture), sent);
        }

        [TestMethod]
        [DataRow("minimal")]
        [DataRow("full")]
        public async Task WatchFiles_SentByTheClient_ValidatesWithKebabKeys(string variant)
        {
            var fixture = (JObject)_bundle.Fixture("watch-files-params", variant, FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();

            harness.Client.WatchFiles(Text(fixture, "repo-root"), RelativePaths(fixture));
            var sent = await WaitForAsync(server.WatchFiles.Task);

            AssertSent("watch-files-params", WithoutBaselineRevision(fixture), sent);
        }

        [TestMethod]
        [DataRow("minimal")]
        [DataRow("full")]
        public async Task StopWatchFiles_SentByTheClient_ValidatesWithKebabKeys(string variant)
        {
            var fixture = (JObject)_bundle.Fixture("stop-watch-files-params", variant, FixtureForm.Wire);
            var (server, harness) = await StartHarnessAsync();

            harness.Client.StopWatchFiles(Text(fixture, "repo-root"), RelativePaths(fixture));
            var sent = await WaitForAsync(server.StopWatchFiles.Task);

            AssertSent("stop-watch-files-params", fixture, sent);
        }

        [TestMethod]
        public void Comparison_WithADroppedFixtureKey_FailsNamingTheKey()
        {
            var fixture = (JObject)_bundle.Fixture("watch-files-params", "full", FixtureForm.Wire);
            var sent = (JObject)WithoutBaselineRevision(fixture).DeepClone();
            sent.Remove("relative-paths");

            var failure = Assert.Throws<AssertFailedException>(() => AssertSent("watch-files-params", WithoutBaselineRevision(fixture), sent));

            StringAssert.Contains(failure.Message, "relative-paths");
        }

        [TestMethod]
        public void Comparison_WithARecasedKey_FailsOnTheCasing()
        {
            var fixture = (JObject)_bundle.Fixture("review-files-params", "minimal", FixtureForm.Wire);
            var sent = (JObject)fixture.DeepClone();
            sent["repoRoot"] = sent["repo-root"];
            sent.Remove("repo-root");

            var failure = Assert.Throws<AssertFailedException>(() => AssertSent("review-files-params", fixture, sent));

            StringAssert.Contains(failure.Message, "repoRoot");
        }

        private static JToken Required(JToken parent, string key)
        {
            return parent[key] ?? throw new InvalidOperationException($"{key} is missing");
        }

        private static string Text(JToken parent, string key)
        {
            return (string?)Required(parent, key) ?? throw new InvalidOperationException($"{key} is null");
        }

        private static JObject WithoutBaselineRevision(JObject fixture)
        {
            var expected = (JObject)fixture.DeepClone();
            expected.Remove("baseline-revision");
            return expected;
        }

        private static IReadOnlyList<string>? RelativePaths(JObject fixture)
        {
            return (fixture["relative-paths"] as JArray)?.Select(path => path.ToString()).ToList();
        }

        private static void AssertSent(string root, JObject expected, JToken sent)
        {
            var (schemaFile, definition) = _bundle.FixtureSchema(root);
            ContractBundle.AssertCasing(sent, Casing.Kebab);
            ContractBundle.AssertDeclaredFieldsMatch(expected, sent, ContractEntityForm.FieldPaths(expected, sent));
            _bundle.Validate(schemaFile, definition, sent);
        }

        private static async Task<T> WaitForAsync<T>(Task<T> task)
        {
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
            if (completed != task)
            {
                throw new TimeoutException("Timed out waiting for the message to reach the server.");
            }

            return await task;
        }

        private async Task<(CapturingServer Server, ContractClientHarness Harness)> StartHarnessAsync()
        {
            var startWire = (JObject)_bundle.Fixture("start-notification", "minimal", FixtureForm.Wire);
            var server = new CapturingServer();
            var harness = ContractClientHarness.Create(startWire, server);
            _harnesses.Add(harness);
            await harness.Client.StartAsync();
            return (server, harness);
        }

        private sealed class CapturingServer
        {
            public TaskCompletionSource<JObject> Telemetry { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> ReviewFiles { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> WatchFiles { get; } = new TaskCompletionSource<JObject>();

            public TaskCompletionSource<JObject> StopWatchFiles { get; } = new TaskCompletionSource<JObject>();

            [JsonRpcMethod("cs-ide/telemetry", UseSingleObjectParameterDeserialization = true)]
            public object? OnTelemetry(JObject parameters)
            {
                Telemetry.TrySetResult(parameters);
                return null;
            }

            [JsonRpcMethod("cs-ide/reviewFiles", UseSingleObjectParameterDeserialization = true)]
            public void OnReviewFiles(JObject parameters)
            {
                ReviewFiles.TrySetResult(parameters);
            }

            [JsonRpcMethod("cs-ide/watchFiles", UseSingleObjectParameterDeserialization = true)]
            public void OnWatchFiles(JObject parameters)
            {
                WatchFiles.TrySetResult(parameters);
            }

            [JsonRpcMethod("cs-ide/stopWatchFiles", UseSingleObjectParameterDeserialization = true)]
            public void OnStopWatchFiles(JObject parameters)
            {
                StopWatchFiles.TrySetResult(parameters);
            }
        }
    }
}
