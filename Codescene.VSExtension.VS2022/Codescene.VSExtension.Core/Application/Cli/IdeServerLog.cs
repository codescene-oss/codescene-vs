// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Models.Cli.Telemetry;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Cli
{
    internal static partial class IdeServerLog
    {
        private static readonly Dictionary<string, Func<object, string>> RequestSummaries =
            new Dictionary<string, Func<object, string>>
            {
                { "cs-ide/telemetry", argument => LogFields.Format(LogFields.Field("eventName", TelemetryEventName(argument))) },
                { "cs-ide/check-rules", SummarizeCheckRulesRequest },
                { "cs-ide/getWatchInventory", SummarizeInventoryRequest },
            };

        private static readonly Dictionary<string, Func<object, string>> ResponseSummaries =
            new Dictionary<string, Func<object, string>>
            {
                { "cs-ide/device-id", argument => LogFields.Format(LogFields.Field("ok", true)) },
                { "cs-ide/code-health-rules-template", argument => LogFields.Format(LogFields.Field("ok", true)) },
                { "cs-ide/check-rules", SummarizeCheckRulesResponse },
                { "cs-ide/getWatchInventory", SummarizeInventoryResponse },
            };

        public static string Sending(int id, string method, string summary)
        {
            return "[cs-ide] sending id=" + id + " method=" + method + Suffix(summary);
        }

        public static string Received(int id, string method, long durationMs, string summary)
        {
            return "[cs-ide] received id=" + id + " method=" + method + " durationMs=" + durationMs + Suffix(summary);
        }

        public static string SummarizeRequest(string method, object argument)
        {
            return Summarize(RequestSummaries, method, argument);
        }

        public static string SummarizeResponse(string method, object result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            return Summarize(ResponseSummaries, method, result);
        }

        public static string ReviewSummary(CliReviewModel result)
        {
            if (result == null)
            {
                return string.Empty;
            }

            return LogFields.Format(
                LogFields.Field("score", result.Score),
                LogFields.Field("fileSmells", result.FileLevelCodeSmells?.Count ?? 0),
                LogFields.Field("functionSmells", result.FunctionLevelCodeSmells?.Count ?? 0),
                LogFields.Field("hasRulesError", result.CodeHealthRulesError != null),
                LogFields.Field("sha", result.GitBlobSha));
        }

        public static string DeltaSummary(DeltaResponseModel result)
        {
            if (result == null)
            {
                return LogFields.Format(LogFields.Field("result", "null"));
            }

            return LogFields.Format(
                LogFields.Field("oldScore", result.OldScore),
                LogFields.Field("newScore", result.NewScore),
                LogFields.Field("scoreChange", result.ScoreChange),
                LogFields.Field("fileFindings", result.FileLevelFindings?.Length ?? 0),
                LogFields.Field("functionFindings", result.FunctionLevelFindings?.Length ?? 0));
        }

        private static string Summarize(Dictionary<string, Func<object, string>> summaries, string method, object value)
        {
            if (method != null && summaries.TryGetValue(method, out var summarize))
            {
                return summarize(value);
            }

            return string.Empty;
        }

        private static string SummarizeCheckRulesRequest(object argument)
        {
            var rules = argument as CheckRulesRequestParams;
            if (rules == null)
            {
                return string.Empty;
            }

            return LogFields.Format(LogFields.Field("repoRoot", rules.RepoRoot), LogFields.Field("path", rules.Path));
        }

        private static string SummarizeInventoryRequest(object argument)
        {
            var inventory = argument as WatchInventoryRequestParams;
            if (inventory == null)
            {
                return string.Empty;
            }

            return LogFields.Format(LogFields.Field("repoRoot", inventory.RepoRoot));
        }

        private static string SummarizeCheckRulesResponse(object result)
        {
            var rules = result as CheckRulesResponse;
            if (rules == null)
            {
                return string.Empty;
            }

            return LogFields.Format(
                LogFields.Field("failed", rules.Failed),
                LogFields.Field("parsingErrorCount", rules.ParsingErrors?.Count ?? 0));
        }

        private static string SummarizeInventoryResponse(object result)
        {
            var inventory = result as WatchInventory;
            if (inventory == null)
            {
                return string.Empty;
            }

            return LogFields.Format(
                LogFields.Field("count", inventory.Files?.Count ?? 0),
                LogFields.Field("files", inventory.Files));
        }

        private static string TelemetryEventName(object argument)
        {
            var payload = argument is TelemetryRequestParams wrapped ? wrapped.Event : argument;
            if (payload is TelemetryEvent telemetry)
            {
                return telemetry.EventName;
            }

            if (payload is JObject obj)
            {
                return (string)obj["event-name"] ?? (string)obj["eventName"];
            }

            return null;
        }

        private static string Suffix(string summary)
        {
            return string.IsNullOrEmpty(summary) ? string.Empty : " " + summary;
        }
    }
}
