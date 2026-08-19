// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Cli.Rpc;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Models.Cli.Telemetry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class IdeServerClientTests
    {
        private static readonly JsonSerializerSettings WireSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
        };

        [TestMethod]
        public void ReviewFiles_SerializesKebabCaseKeys()
        {
            var json = JsonConvert.SerializeObject(
                new ReviewFilesParams
                {
                    RepoRoot = @"C:\repo",
                    BaselineRevision = "abc123",
                    Files = new List<ReviewFileItem>
                    {
                        new ReviewFileItem { Id = "id-1", RelPath = "src/a.cs", Content = "class A {}" },
                    },
                },
                WireSettings);
            var payload = JObject.Parse(json);

            Assert.AreEqual(@"C:\repo", payload.Value<string>("repo-root"));
            Assert.AreEqual("abc123", payload.Value<string>("baseline-revision"));
            var files = payload["files"] as JArray;
            Assert.IsNotNull(files);
            Assert.AreEqual("src/a.cs", files[0].Value<string>("rel-path"));
            Assert.IsNull(payload["repoRoot"]);
        }

        [TestMethod]
        public void WatchFiles_SerializesKebabCaseKeys()
        {
            var json = JsonConvert.SerializeObject(
                new WatchFilesParams { RepoRoot = @"C:\repo", BaselineRevision = "base" },
                WireSettings);
            var payload = JObject.Parse(json);

            Assert.AreEqual(@"C:\repo", payload.Value<string>("repo-root"));
            Assert.AreEqual("base", payload.Value<string>("baseline-revision"));
            Assert.IsNull(payload["repoRoot"]);
        }

        [TestMethod]
        public void DeviceId_DeserializesKebabCaseField()
        {
            var response = RpcJsonNormalizer.Deserialize<DeviceIdResponse>(JObject.Parse("{\"device-id\":\"device-42\"}"));
            Assert.AreEqual("device-42", response.DeviceId);
        }

        [TestMethod]
        public void Preflight_SerializesForceFlag()
        {
            var json = JsonConvert.SerializeObject(new PreflightRequestParams { Force = true }, WireSettings);
            Assert.IsTrue(JObject.Parse(json).Value<bool>("force"));
        }

        [TestMethod]
        public void Telemetry_WrapsEventWithKebabCaseName()
        {
            var json = JsonConvert.SerializeObject(
                new TelemetryRequest { Event = new TelemetryEvent { EventName = "vs/test" } },
                WireSettings);
            var payload = JObject.Parse(json);
            var eventToken = payload["event"];
            Assert.IsNotNull(eventToken);
            Assert.AreEqual("vs/test", eventToken.Value<string>("event-name"));
        }

        [TestMethod]
        public void Review_SerializesFileContentKey()
        {
            var json = JsonConvert.SerializeObject(
                new ReviewRequestModel { FilePath = "a.cs", FileContent = "code" },
                WireSettings);
            var payload = JObject.Parse(json);
            Assert.AreEqual("a.cs", payload.Value<string>("path"));
            Assert.AreEqual("code", payload.Value<string>("file-content"));
        }

        [TestMethod]
        public void Refactor_SerializesFnToRefactorKey()
        {
            var json = JsonConvert.SerializeObject(
                new RefactorPostRequestModel { Token = "tok", FnToRefactorNippyB64 = "abc" },
                WireSettings);
            var payload = JObject.Parse(json);
            Assert.AreEqual("tok", payload.Value<string>("token"));
            Assert.AreEqual("abc", payload.Value<string>("fn-to-refactor-nippy-b64"));
        }

        [TestMethod]
        public void FileReview_CamelCaseInbound_IsAccepted()
        {
            var token = JObject.Parse("{\"id\":\"1\",\"path\":\"src/a.cs\",\"repoRoot\":\"C:\\\\repo\",\"result\":{\"score\":8,\"gitBlobSha\":\"sha\"}}");
            var received = RpcJsonNormalizer.Deserialize<FileReviewNotification>(token);

            Assert.AreEqual(@"C:\repo", received.RepoRoot);
            Assert.AreEqual("sha", received.Result.GitBlobSha);
        }
    }
}
