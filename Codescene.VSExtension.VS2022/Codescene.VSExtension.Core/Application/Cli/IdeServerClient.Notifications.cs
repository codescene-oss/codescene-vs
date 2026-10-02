// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Cli
{
    public sealed partial class IdeServerClient
    {
        private void HandleStart(JObject metadata)
        {
            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(metadata ?? new JObject());
            var parsed = new ServerMetadata
            {
                Sha = (string)normalized["sha"],
                Version = (string)normalized["version"],
            };
            Metadata = parsed;
            TaskCompletionSource<ServerMetadata> pendingStart;
            lock (_gate)
            {
                pendingStart = _pendingStart;
                _pendingStart = null;
            }

            pendingStart?.TrySetResult(parsed);
            var restart = Interlocked.Increment(ref _startCount) > 1;
            _logger.Info("[cs-ide] received start " + LogFields.Format(
                LogFields.Field("version", parsed.Version),
                LogFields.Field("sha", parsed.Sha),
                LogFields.Field("restart", restart)));
            ServerStarted?.Invoke(this, new ServerStartEvent { Metadata = parsed, Restart = restart });
        }

        private void HandleReview(JObject notification)
        {
            var identity = ParseIdentity(notification, "fileReview");
            if (identity == null)
            {
                return;
            }

            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(notification);
            var queue = ParseQueue(normalized, identity.Item3);
            EmitQueue(queue);
            var result = normalized["result"]?.ToObject<CliReviewModel>(Serializer);
            _logger.Info("[cs-ide] received fileReview " + LogFields.Format(
                LogFields.Field("id", identity.Item1 ?? "(none)"),
                LogFields.Field("path", identity.Item2),
                LogFields.Field("repo", identity.Item3)) + " " + IdeServerLog.ReviewSummary(result));
            ReviewReceived?.Invoke(this, new ReviewNotification
            {
                Id = identity.Item1,
                Path = identity.Item2,
                RepoRoot = identity.Item3,
                Result = result,
                Queue = queue,
            });
        }

        private void HandleDelta(JObject notification)
        {
            var identity = ParseIdentity(notification, "deltaReview");
            if (identity == null)
            {
                return;
            }

            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(notification);
            var queue = ParseQueue(normalized, identity.Item3);
            EmitQueue(queue);
            var resultToken = normalized["result"];
            var delta = resultToken == null || resultToken.Type == JTokenType.Null
                ? null
                : resultToken.ToObject<DeltaResponseModel>(Serializer);
            _logger.Info("[cs-ide] received deltaReview " + LogFields.Format(
                LogFields.Field("id", identity.Item1 ?? "(none)"),
                LogFields.Field("path", identity.Item2),
                LogFields.Field("repo", identity.Item3)) + " " + IdeServerLog.DeltaSummary(delta));
            DeltaReceived?.Invoke(this, new DeltaNotification
            {
                Id = identity.Item1,
                Path = identity.Item2,
                RepoRoot = identity.Item3,
                Result = delta,
                Queue = queue,
            });
        }

        private void HandleReviewFailure(JObject notification)
        {
            var identity = ParseIdentity(notification, "reviewFailed");
            if (identity == null)
            {
                return;
            }

            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(notification);
            var queue = ParseQueue(normalized, identity.Item3);
            EmitQueue(queue);
            ReviewFailed?.Invoke(this, new ReviewFailedNotification
            {
                Id = identity.Item1,
                Path = identity.Item2,
                RepoRoot = identity.Item3,
                Message = (string)normalized["message"],
                Queue = queue,
            });
        }

        private void HandleWatchInventory(JObject notification)
        {
            var inventory = ParseWatchInventory(notification, null);
            if (string.IsNullOrEmpty(inventory.RepoRoot))
            {
                _logger.Info("[cs-ide] ignoring watchInventoryChanged reason=missing-repo");
                return;
            }

            _logger.Info("[cs-ide] received watchInventoryChanged " + LogFields.Format(
                LogFields.Field("repo", inventory.RepoRoot),
                LogFields.Field("count", inventory.Files.Count),
                LogFields.Field("files", inventory.Files)));
            WatchInventoryChanged?.Invoke(this, inventory);
        }

        private Tuple<string, string, string> ParseIdentity(JObject notification, string kind)
        {
            if (notification == null)
            {
                _logger.Info("[cs-ide] ignoring " + kind + " reason=missing-notification");
                return null;
            }

            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(notification);
            var path = (string)normalized["path"];
            var repoRoot = (string)normalized["repo-root"];
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(repoRoot))
            {
                _logger.Info("[cs-ide] ignoring " + kind + " reason=missing-path-or-repo");
                return null;
            }

            var idToken = normalized["id"];
            string id = null;
            if (idToken != null && idToken.Type != JTokenType.Null)
            {
                id = (string)idToken;
            }

            return Tuple.Create(id, path, repoRoot);
        }

        private WatchInventory ParseWatchInventory(JObject notification, string fallbackRepoRoot)
        {
            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(notification ?? new JObject());
            var files = normalized["files"] as JArray;
            return new WatchInventory
            {
                RepoRoot = (string)normalized["repo-root"] ?? fallbackRepoRoot,
                Files = files == null ? new List<string>() : files.Select(token => (string)token).Where(value => value != null).ToList(),
            };
        }

        private ReviewQueue ParseQueue(JObject notification, string repoRoot)
        {
            var queueToken = notification?["queue"] as JObject;
            if (queueToken == null)
            {
                return null;
            }

            var files = queueToken["files"] as JArray;
            return new ReviewQueue
            {
                Count = queueToken["count"]?.Value<int>() ?? 0,
                Files = (files ?? new JArray())
                    .Select(token => (string)token)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .Select(relPath => Path.Combine(repoRoot ?? string.Empty, relPath.Replace('/', Path.DirectorySeparatorChar)))
                    .ToList(),
            };
        }

        private void EmitQueue(ReviewQueue queue)
        {
            if (queue == null)
            {
                return;
            }

            QueueChanged?.Invoke(this, queue);
        }
    }
}
