// Copyright (c) CodeScene. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Codescene.VSExtension.Core.Application.Cli.Rpc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Codescene.VSExtension.Core.Tests
{
    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204", Justification = "Private static helpers follow the instance methods that use them.")]
    public sealed class FakeIdeServer : IDisposable
    {
        private readonly JsonRpc _rpc;
        private readonly Stream _stream;
        private bool _disposed;

        public FakeIdeServer(Stream stream, string sha = "fixture-sha", string version = "fixture-version")
        {
            _stream = stream;
            var formatter = new JsonMessageFormatter(Encoding.UTF8);
            var handler = new HeaderDelimitedMessageHandler(stream, stream, formatter);
            _rpc = new JsonRpc(handler);
            Sha = sha;
            Version = version;
            _rpc.AddLocalRpcTarget(
                this,
                new JsonRpcTargetOptions
                {
                    UseSingleObjectParameterDeserialization = true,
                });
        }

        public string Sha { get; }

        public string Version { get; }

        public JToken? LastRequest { get; private set; }

        public JToken? LastNotification { get; private set; }

        public string? LastNotificationMethod { get; private set; }

        public List<JToken> ReviewFilesNotifications { get; } = new List<JToken>();

        public List<JToken> WatchFilesNotifications { get; } = new List<JToken>();

        public List<JToken> StopWatchFilesNotifications { get; } = new List<JToken>();

        public void Start()
        {
            _rpc.StartListening();
            _ = _rpc.NotifyWithParameterObjectAsync(
                "cs-ide/start",
                new JObject { ["sha"] = Sha, ["version"] = Version });
        }

        public Task EmitFileReviewAsync(JObject payload)
        {
            return _rpc.NotifyWithParameterObjectAsync("cs-ide/fileReview", payload);
        }

        public Task EmitDeltaReviewAsync(JObject payload)
        {
            return _rpc.NotifyWithParameterObjectAsync("cs-ide/deltaReview", payload);
        }

        public Task EmitReviewFailedAsync(JObject payload)
        {
            return _rpc.NotifyWithParameterObjectAsync("cs-ide/reviewFailed", payload);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _rpc.Dispose();
            _stream.Dispose();
        }

        [JsonRpcMethod("cs-ide/review")]
        public JObject OnReview(JToken token)
        {
            LastRequest = RpcJsonNormalizer.Normalize(Unwrap(token));
            return new JObject
            {
                ["score"] = 9.5,
                ["raw-score"] = "raw",
                ["file-level-code-smells"] = new JArray(),
                ["function-level-code-smells"] = new JArray(),
            };
        }

        [JsonRpcMethod("cs-ide/delta")]
        public JObject OnDelta(JToken token)
        {
            LastRequest = RpcJsonNormalizer.Normalize(Unwrap(token));
            return new JObject
            {
                ["old-score"] = 10,
                ["new-score"] = 9.5,
                ["score-change"] = -0.5,
                ["file-level-findings"] = new JArray(),
                ["function-level-findings"] = new JArray(),
            };
        }

        [JsonRpcMethod("cs-ide/preflight")]
        public JObject OnPreflight(JToken token)
        {
            LastRequest = RpcJsonNormalizer.Normalize(Unwrap(token));
            return new JObject { ["version"] = 2, ["fileTypes"] = new JArray("cs") };
        }

        [JsonRpcMethod("cs-ide/fns-to-refactor")]
        public JArray OnFnsToRefactor(JToken token)
        {
            LastRequest = RpcJsonNormalizer.Normalize(Unwrap(token));
            return new JArray();
        }

        [JsonRpcMethod("cs-ide/refactor")]
        public JObject OnRefactor(JToken token)
        {
            LastRequest = RpcJsonNormalizer.Normalize(Unwrap(token));
            return new JObject { ["code"] = "refactored" };
        }

        [JsonRpcMethod("cs-ide/telemetry")]
        public JObject OnTelemetry(JToken token)
        {
            LastRequest = RpcJsonNormalizer.Normalize(Unwrap(token));
            return new JObject { ["status"] = 202 };
        }

        [JsonRpcMethod("cs-ide/device-id")]
        public JObject OnDeviceId(JToken token)
        {
            LastRequest = RpcJsonNormalizer.Normalize(Unwrap(token));
            return new JObject { ["device-id"] = "device-42" };
        }

        [JsonRpcMethod("cs-ide/reviewFiles")]
        public void OnReviewFiles(JToken token)
        {
            var normalized = RpcJsonNormalizer.Normalize(Unwrap(token));
            LastNotification = normalized;
            LastNotificationMethod = "cs-ide/reviewFiles";
            ReviewFilesNotifications.Add(normalized);
            _ = EmitReviewsForFilesAsync(normalized);
        }

        [JsonRpcMethod("cs-ide/watchFiles")]
        public void OnWatchFiles(JToken token)
        {
            var normalized = RpcJsonNormalizer.Normalize(Unwrap(token));
            LastNotification = normalized;
            LastNotificationMethod = "cs-ide/watchFiles";
            WatchFilesNotifications.Add(normalized);
        }

        [JsonRpcMethod("cs-ide/stopWatchFiles")]
        public void OnStopWatchFiles(JToken token)
        {
            var normalized = RpcJsonNormalizer.Normalize(Unwrap(token));
            LastNotification = normalized;
            LastNotificationMethod = "cs-ide/stopWatchFiles";
            StopWatchFilesNotifications.Add(normalized);
        }

        private async Task EmitReviewsForFilesAsync(JToken notification)
        {
            var files = notification["files"] as JArray;
            if (files == null)
            {
                return;
            }

            var repoRoot = notification.Value<string>("repo-root");
            foreach (var file in files)
            {
                var id = file.Value<string>("id");
                var relPath = file.Value<string>("rel-path");
                var content = file.Value<string>("content") ?? string.Empty;
                if (content == "fail")
                {
                    await EmitReviewFailedAsync(new JObject
                    {
                        ["id"] = id,
                        ["path"] = relPath,
                        ["repo-root"] = repoRoot,
                        ["message"] = "fixture review failed",
                    });
                    continue;
                }

                var sha = GitBlobSha.FromUtf8(content);
                await EmitFileReviewAsync(new JObject
                {
                    ["id"] = id,
                    ["path"] = relPath,
                    ["repo-root"] = repoRoot,
                    ["result"] = new JObject
                    {
                        ["score"] = 8.5,
                        ["raw-score"] = "raw",
                        ["git-blob-sha"] = sha,
                        ["file-level-code-smells"] = new JArray(),
                        ["function-level-code-smells"] = new JArray(),
                    },
                });
                await EmitDeltaReviewAsync(new JObject
                {
                    ["id"] = id,
                    ["path"] = relPath,
                    ["repo-root"] = repoRoot,
                    ["result"] = new JObject
                    {
                        ["old-score"] = 10,
                        ["new-score"] = 8.5,
                        ["score-change"] = -1.5,
                        ["new-git-blob-sha"] = sha,
                        ["file-level-findings"] = new JArray(),
                        ["function-level-findings"] = new JArray(),
                    },
                });
            }
        }

        private static JToken Unwrap(JToken token)
        {
            if (token is JArray array && array.Count == 1)
            {
                return array[0];
            }

            return token;
        }
    }
}
