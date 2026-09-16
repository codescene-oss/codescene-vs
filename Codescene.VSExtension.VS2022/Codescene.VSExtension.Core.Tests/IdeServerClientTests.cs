// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using System.Security.Cryptography;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Moq;
using Nerdbank.Streams;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class IdeServerClientTests
    {
        private string _tempFilePath;
        private Mock<ICliSettingsProvider> _settings;
        private Mock<ILogger> _logger;
        private Mock<ISettingsProvider> _userSettings;

        [TestInitialize]
        public void Setup()
        {
            _tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe");
            File.WriteAllText(_tempFilePath, "dummy-cli");
            _settings = new Mock<ICliSettingsProvider>();
            _logger = new Mock<ILogger>();
            _userSettings = new Mock<ISettingsProvider>();
            _settings.Setup(x => x.CliFileFullPath).Returns(_tempFilePath);
            _settings.Setup(x => x.RequiredDevToolVersion).Returns("fixture-sha");
            _settings.Setup(x => x.RequiredCliBinarySha256).Returns(Sha256(_tempFilePath));
            _userSettings.Setup(x => x.ServerWorkerThreads).Returns(0);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }

        [TestMethod]
        public async Task StartAsync_FileDoesNotExist_ThrowsFileNotFoundException()
        {
            File.Delete(_tempFilePath);
            using var client = CreateClient(new FakeProcessFactory(new FakeIdeServerProcess(Stream.Null, Stream.Null)));

            var exception = await Assert.ThrowsAsync<FileNotFoundException>(() => client.StartAsync());

            Assert.Contains("CodeScene CLI executable not found", exception.Message);
        }

        [TestMethod]
        public async Task StartAsync_WaitsForStartNotificationAndLaunchesServerCommand()
        {
            using var harness = await IdeServerTestHarness.StartAsync(_settings, _logger, _userSettings);
            using var client = harness.Client;

            var metadata = await client.StartAsync();

            Assert.AreEqual("fixture-sha", metadata.Sha);
            Assert.AreEqual("fixture-version", metadata.Version);
            Assert.StartsWith("server --threads ", harness.Factory.LastArguments);
        }

        [TestMethod]
        public async Task StartAsync_WhenShaDoesNotMatch_Throws()
        {
            _settings.Setup(x => x.RequiredDevToolVersion).Returns("other-sha");
            using var harness = await IdeServerTestHarness.StartAsync(_settings, _logger, _userSettings);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Client.StartAsync());

            Assert.Contains("version mismatch", exception.Message);
        }

        [TestMethod]
        public async Task ReviewAsync_NormalizesCamelCaseResponse()
        {
            using var harness = await IdeServerTestHarness.StartAsync(_settings, _logger, _userSettings);
            await harness.Client.StartAsync();

            var review = await harness.Client.ReviewAsync(new ReviewRequestModel
            {
                FilePath = "src/a.cs",
                FileContent = "class A {}",
            });

            Assert.AreEqual("raw", review.RawScore);
            Assert.AreEqual(9.68f, review.Score);
            Assert.AreEqual("review-sha", review.GitBlobSha);
        }

        [TestMethod]
        public async Task RepeatedReviews_UseSingleProcess()
        {
            using var harness = await IdeServerTestHarness.StartAsync(_settings, _logger, _userSettings);
            await harness.Client.StartAsync();

            await harness.Client.ReviewAsync(new ReviewRequestModel { FilePath = "src/a.cs", FileContent = "class A {}" });
            await harness.Client.ReviewAsync(new ReviewRequestModel { FilePath = "src/b.cs", FileContent = "class B {}" });

            Assert.AreEqual(1, harness.Factory.StartCount);
        }

        [TestMethod]
        public async Task DeviceIdAsync_ReadsCamelCaseOrKebabCase()
        {
            using var harness = await IdeServerTestHarness.StartAsync(_settings, _logger, _userSettings);
            await harness.Client.StartAsync();

            var deviceId = await harness.Client.DeviceIdAsync();

            Assert.AreEqual("device-42", deviceId);
        }

        [TestMethod]
        public async Task WatchFiles_RaisesInventoryAndReviewNotifications()
        {
            using var harness = await IdeServerTestHarness.StartAsync(_settings, _logger, _userSettings);
            await harness.Client.StartAsync();
            var inventoryReceived = new TaskCompletionSource<WatchInventory>();
            var reviewReceived = new TaskCompletionSource<ReviewNotification>();
            harness.Client.WatchInventoryChanged += (_, inventory) => inventoryReceived.TrySetResult(inventory);
            harness.Client.ReviewReceived += (_, review) => reviewReceived.TrySetResult(review);

            harness.Client.WatchFiles("C:/repo", new[] { "src" });

            var inventory = await WaitForAsync(inventoryReceived.Task);
            var review = await WaitForAsync(reviewReceived.Task);
            Assert.AreEqual("C:/repo", inventory.RepoRoot);
            Assert.AreEqual("watched.ts", inventory.Files[0]);
            Assert.IsNull(review.Id);
            Assert.AreEqual("watched.ts", review.Path);
        }

        [TestMethod]
        public async Task ReviewFiles_EchoesIdAndQueue()
        {
            using var harness = await IdeServerTestHarness.StartAsync(_settings, _logger, _userSettings);
            await harness.Client.StartAsync();
            var reviewReceived = new TaskCompletionSource<ReviewNotification>();
            var queueReceived = new TaskCompletionSource<ReviewQueue>();
            harness.Client.ReviewReceived += (_, review) => reviewReceived.TrySetResult(review);
            harness.Client.QueueChanged += (_, queue) => queueReceived.TrySetResult(queue);

            var files = new[] { new ReviewFile { Id = "req-1", RelPath = "src/a.ts", Content = "hello" } };
            harness.Client.ReviewFiles("C:/repo", files);

            var review = await WaitForAsync(reviewReceived.Task);
            var queue = await WaitForAsync(queueReceived.Task);
            Assert.AreEqual("req-1", review.Id);
            Assert.AreEqual("src/a.ts", review.Path);
            Assert.AreEqual(GitBlobSha.FromUtf8("hello"), review.Result.GitBlobSha);
            Assert.AreEqual(2, queue.Count);
        }

        [TestMethod]
        public async Task ReviewFailed_TreatsNullIdAsMissing()
        {
            using var harness = await IdeServerTestHarness.StartAsync(_settings, _logger, _userSettings);
            await harness.Client.StartAsync();
            var failed = new TaskCompletionSource<ReviewFailedNotification>();
            harness.Client.ReviewFailed += (_, notification) => failed.TrySetResult(notification);

            var files = new[] { new ReviewFile { RelPath = "src/a.ts", Content = "fail" } };
            harness.Client.ReviewFiles("C:/repo", files);

            var notification = await WaitForAsync(failed.Task);
            Assert.IsNull(notification.Id);
            Assert.AreEqual("src/a.ts", notification.Path);
        }

        private IdeServerClient CreateClient(IIdeServerProcessFactory factory)
        {
            return new IdeServerClient(
                _settings.Object,
                _logger.Object,
                factory,
                _userSettings.Object,
                TimeSpan.FromSeconds(2));
        }

        private async Task<T> WaitForAsync<T>(Task<T> task)
        {
            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
            if (completed != task)
            {
                throw new TimeoutException("Timed out waiting for server notification.");
            }

            return await task;
        }

        private string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }
    }

    internal sealed class IdeServerTestHarness : IDisposable
    {
        private IdeServerTestHarness(IdeServerClient client, FakeProcessFactory factory, JsonRpc serverRpc, Stream serverStream)
        {
            Client = client;
            Factory = factory;
            ServerRpc = serverRpc;
            ServerStream = serverStream;
        }

        public IdeServerClient Client { get; }

        public FakeProcessFactory Factory { get; }

        public JsonRpc ServerRpc { get; }

        public Stream ServerStream { get; }

        public static async Task<IdeServerTestHarness> StartAsync(
            Mock<ICliSettingsProvider> settings,
            Mock<ILogger> logger,
            Mock<ISettingsProvider> userSettings)
        {
            var (clientStream, serverStream) = FullDuplexStream.CreatePair();
            var process = new FakeIdeServerProcess(clientStream, clientStream);
            var factory = new FakeProcessFactory(process);
            var serverRpc = new JsonRpc(new HeaderDelimitedMessageHandler(serverStream, serverStream));
            serverRpc.AddLocalRpcTarget(new FixtureServer(serverRpc));
            serverRpc.StartListening();
            var client = new IdeServerClient(settings.Object, logger.Object, factory, userSettings.Object, TimeSpan.FromSeconds(5));
            var harness = new IdeServerTestHarness(client, factory, serverRpc, serverStream);
            _ = Task.Run(async () =>
            {
                await Task.Yield();
                await serverRpc.NotifyWithParameterObjectAsync("cs-ide/start", new { sha = "fixture-sha", version = "fixture-version" });
            });
            return harness;
        }

        public void Dispose()
        {
            Client.Dispose();
            ServerRpc.Dispose();
            ServerStream.Dispose();
        }
    }

    internal sealed class FixtureServer
    {
        private readonly JsonRpc _rpc;

        public FixtureServer(JsonRpc rpc)
        {
            _rpc = rpc;
        }

        [JsonRpcMethod("cs-ide/review", UseSingleObjectParameterDeserialization = true)]
        public JObject Review(JObject request)
        {
            return JObject.Parse(@"{""fileLevelCodeSmells"":[],""functionLevelCodeSmells"":[],""rawScore"":""raw"",""score"":9.68,""gitBlobSha"":""review-sha""}");
        }

        [JsonRpcMethod("cs-ide/device-id", UseSingleObjectParameterDeserialization = true)]
        public JObject DeviceId(JObject request)
        {
            return JObject.Parse(@"{""deviceId"":""device-42""}");
        }

        [JsonRpcMethod("cs-ide/watchFiles", UseSingleObjectParameterDeserialization = true)]
        public async Task WatchFiles(JObject parameters)
        {
            var repoRoot = (string)parameters["repo-root"];
            await _rpc.NotifyWithParameterObjectAsync("cs-ide/watchInventoryChanged", new { repoRoot, files = new[] { "watched.ts" } });
            await _rpc.NotifyWithParameterObjectAsync("cs-ide/fileReview", new
            {
                path = "watched.ts",
                repoRoot,
                result = new { fileLevelCodeSmells = Array.Empty<object>(), functionLevelCodeSmells = Array.Empty<object>(), rawScore = "raw", score = 9.68, gitBlobSha = "watch-sha" },
            });
        }

        [JsonRpcMethod("cs-ide/reviewFiles", UseSingleObjectParameterDeserialization = true)]
        public async Task ReviewFiles(JObject parameters)
        {
            var repoRoot = (string)parameters["repo-root"];
            foreach (var file in (JArray)parameters["files"])
            {
                var content = (string)file["content"] ?? string.Empty;
                var relPath = (string)file["rel-path"];
                var id = file["id"]?.Type == JTokenType.Null ? null : (string)file["id"];
                if (content == "fail")
                {
                    var failed = new JObject
                    {
                        ["id"] = null,
                        ["path"] = relPath,
                        ["repoRoot"] = repoRoot,
                        ["message"] = "fixture review failed",
                    };
                    await _rpc.NotifyAsync("cs-ide/reviewFailed", failed);
                    continue;
                }

                var payload = new JObject
                {
                    ["path"] = relPath,
                    ["repoRoot"] = repoRoot,
                    ["result"] = new JObject
                    {
                        ["rawScore"] = "raw",
                        ["score"] = 9.68,
                        ["gitBlobSha"] = GitBlobSha.FromUtf8(content),
                    },
                    ["queue"] = new JObject
                    {
                        ["count"] = 2,
                        ["files"] = new JArray("b.ts", "c.ts"),
                    },
                };
                if (!string.IsNullOrEmpty(id))
                {
                    payload["id"] = id;
                }

                await _rpc.NotifyAsync("cs-ide/fileReview", payload);
            }
        }
    }

    internal sealed class FakeProcessFactory : IIdeServerProcessFactory
    {
        private readonly IIdeServerProcess _process;

        public FakeProcessFactory(IIdeServerProcess process)
        {
            _process = process;
        }

        public string LastFileName { get; private set; }

        public string LastArguments { get; private set; }

        public int StartCount { get; private set; }

        public IIdeServerProcess Start(string fileName, string arguments)
        {
            LastFileName = fileName;
            LastArguments = arguments;
            StartCount++;
            return _process;
        }
    }

    internal sealed class FakeIdeServerProcess : IIdeServerProcess
    {
        public FakeIdeServerProcess(Stream input, Stream output)
        {
            StandardInput = input;
            StandardOutput = output;
            StandardError = Stream.Null;
        }

        public event EventHandler Exited;

        public event EventHandler<IdeServerErrorDataEventArgs> ErrorDataReceived;

        public Stream StandardInput { get; }

        public Stream StandardOutput { get; }

        public Stream StandardError { get; }

        public int Id => 1;

        public bool HasExited { get; private set; }

        public void BeginErrorReadLine()
        {
        }

        public void Kill()
        {
            HasExited = true;
            Exited?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            Kill();
        }
    }
}
