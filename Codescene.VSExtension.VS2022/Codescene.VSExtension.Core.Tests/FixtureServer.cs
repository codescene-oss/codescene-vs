// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Cli;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Codescene.VSExtension.Core.Tests
{
    internal sealed class FixtureServer
    {
        private readonly JsonRpc _rpc;

        public FixtureServer(JsonRpc rpc)
        {
            _rpc = rpc;
        }

        public TaskCompletionSource<JObject> StopWatchReceived { get; } = new TaskCompletionSource<JObject>();

        [JsonRpcMethod("cs-ide/review", UseSingleObjectParameterDeserialization = true)]
        public JObject Review(JObject request)
        {
            if (string.Equals((string?)request["file-content"], "trigger-http-401", StringComparison.Ordinal))
            {
                throw new LocalRpcException("Request failed [401]");
            }

            return JObject.Parse(@"{""fileLevelCodeSmells"":[],""functionLevelCodeSmells"":[],""rawScore"":""raw"",""score"":9.68,""gitBlobSha"":""review-sha""}");
        }

        [JsonRpcMethod("cs-ide/device-id", UseSingleObjectParameterDeserialization = true)]
        public JObject DeviceId(JObject request)
        {
            return JObject.Parse(@"{""deviceId"":""device-42""}");
        }

        [JsonRpcMethod("cs-ide/telemetry", UseSingleObjectParameterDeserialization = true)]
        public object? Telemetry(JObject request)
        {
            return null;
        }

        [JsonRpcMethod("cs-ide/code-health-rules-template", UseSingleObjectParameterDeserialization = true)]
        public JObject CodeHealthRulesTemplate(JObject request)
        {
            return JObject.Parse(@"{""template"":""rules""}");
        }

        [JsonRpcMethod("cs-ide/check-rules", UseSingleObjectParameterDeserialization = true)]
        public async Task<JObject> CheckRules(JObject request, CancellationToken cancellationToken)
        {
            if (string.Equals((string?)request["path"], "fail-401", StringComparison.Ordinal))
            {
                throw new LocalRpcException("Request failed [401]");
            }

            if (string.Equals((string?)request["path"], "delay-cancel", StringComparison.Ordinal))
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }

            return JObject.Parse(@"{""result"":""ok"",""failed"":false,""parsingErrors"":[]}");
        }

        [JsonRpcMethod("cs-ide/getWatchInventory", UseSingleObjectParameterDeserialization = true)]
        public JObject GetWatchInventory(JObject request)
        {
            return JObject.Parse(@"{""files"":[""src/a.cs""]}");
        }

        [JsonRpcMethod("cs-ide/stopWatchFiles", UseSingleObjectParameterDeserialization = true)]
        public void StopWatchFiles(JObject parameters)
        {
            StopWatchReceived.TrySetResult(parameters);
        }

        [JsonRpcMethod("cs-ide/watchFiles", UseSingleObjectParameterDeserialization = true)]
        public async Task WatchFiles(JObject parameters)
        {
            var repoRoot = (string?)parameters["repo-root"] ?? string.Empty;
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
            var repoRoot = (string?)parameters["repo-root"] ?? string.Empty;
            foreach (var file in (JArray?)parameters["files"] ?? new JArray())
            {
                var content = (string?)file["content"] ?? string.Empty;
                var relPath = (string?)file["rel-path"] ?? string.Empty;
                if (content == "fail")
                {
                    await NotifyReviewFailedAsync(relPath, repoRoot);
                    continue;
                }

                await NotifyFileReviewAsync(relPath, repoRoot, content, IdOf(file));
            }
        }

        private static string? IdOf(JToken file)
        {
            return file["id"]?.Type == JTokenType.Null ? null : (string?)file["id"];
        }

        private async Task NotifyReviewFailedAsync(string relPath, string repoRoot)
        {
            await _rpc.NotifyWithParameterObjectAsync("cs-ide/reviewProgress", new
            {
                count = 0,
                done = 3,
                files = Array.Empty<string>(),
            });
            var failed = new JObject
            {
                ["id"] = null,
                ["path"] = relPath,
                ["repoRoot"] = repoRoot,
                ["message"] = "fixture review failed",
            };
            await _rpc.NotifyAsync("cs-ide/reviewFailed", failed);
        }

        private async Task NotifyFileReviewAsync(string relPath, string repoRoot, string content, string? id)
        {
            await _rpc.NotifyWithParameterObjectAsync("cs-ide/reviewProgress", new
            {
                count = 2,
                done = 1,
                files = new[] { "b.ts", "c.ts" },
            });
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
            };
            if (!string.IsNullOrEmpty(id))
            {
                payload["id"] = id;
            }

            await _rpc.NotifyAsync("cs-ide/fileReview", payload);
        }
    }
}
