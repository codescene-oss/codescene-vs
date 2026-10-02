// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Models.Cli.Telemetry;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Cli
{
    internal static class IdeServerLog
    {
        private static readonly Dictionary<string, Func<object, string>> RequestSummaries =
            new Dictionary<string, Func<object, string>>
            {
                { "cs-ide/review", SummarizeReviewRequest },
                { "cs-ide/delta", SummarizeDeltaRequest },
                { "cs-ide/preflight", SummarizePreflightRequest },
                { "cs-ide/fns-to-refactor", argument => SummarizeFnsRequest(argument as FnsToRefactorRequestModel) },
                { "cs-ide/refactor", argument => SummarizeRefactorRequest(argument as RefactorPostRequestModel) },
                { "cs-ide/telemetry", argument => LogFields.Format(LogFields.Field("eventName", TelemetryEventName(argument))) },
                { "cs-ide/check-rules", SummarizeCheckRulesRequest },
                { "cs-ide/getWatchInventory", SummarizeInventoryRequest },
            };

        private static readonly Dictionary<string, Func<object, string>> ResponseSummaries =
            new Dictionary<string, Func<object, string>>
            {
                { "cs-ide/review", argument => ReviewSummary(argument as CliReviewModel) },
                { "cs-ide/delta", argument => DeltaSummary(argument as DeltaResponseModel) },
                { "cs-ide/preflight", SummarizePreflightResponse },
                { "cs-ide/refactor", SummarizeRefactorResponse },
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
            if (method == "cs-ide/fns-to-refactor")
            {
                return SummarizeFnsResponse(result);
            }

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

        private static string SummarizeReviewRequest(object argument)
        {
            var review = argument as ReviewRequestModel;
            if (review == null)
            {
                return string.Empty;
            }

            return LogFields.Format(
                LogFields.Field("path", review.FilePath),
                LogFields.Field("hasContent", review.FileContent != null),
                LogFields.Field("hasCachePath", review.CachePath != null));
        }

        private static string SummarizeDeltaRequest(object argument)
        {
            var delta = argument as DeltaRequestParams;
            if (delta == null)
            {
                return string.Empty;
            }

            return LogFields.Format(
                LogFields.Field("hasOldScore", delta.OldScore != null),
                LogFields.Field("hasNewScore", delta.NewScore != null));
        }

        private static string SummarizePreflightRequest(object argument)
        {
            var preflight = argument as PreflightRequestParams;
            return LogFields.Format(LogFields.Field("force", preflight?.Force == true));
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

        private static string SummarizeFnsResponse(object result)
        {
            var functions = result as IList<FnToRefactorModel>;
            if (functions == null)
            {
                return string.Empty;
            }

            return LogFields.Format(
                LogFields.Field("count", functions.Count),
                LogFields.Field("functions", functions.Select(SummarizeFunction).ToArray()));
        }

        private static string SummarizePreflightResponse(object result)
        {
            var preflight = result as PreFlightResponseModel;
            if (preflight == null)
            {
                return string.Empty;
            }

            return LogFields.Format(
                LogFields.Field("version", preflight.Version),
                LogFields.Field("fileTypes", preflight.FileTypes),
                LogFields.Field("codeSmellCount", preflight.LanguageCommon?.CodeSmells?.Length ?? 0),
                LogFields.Field("maxInputLoc", preflight.LanguageCommon?.MaxInputLoc));
        }

        private static string SummarizeRefactorResponse(object result)
        {
            var refactor = result as RefactorResponseModel;
            if (refactor == null)
            {
                return string.Empty;
            }

            return LogFields.Format(
                LogFields.Field("traceId", refactor.TraceId),
                LogFields.Field("confidence", refactor.Confidence?.Level),
                LogFields.Field("cached", refactor.Metadata?.Cached == true),
                LogFields.Field("reasonCount", refactor.Reasons?.Length ?? 0),
                LogFields.Field("added", refactor.RefactoringProperties?.AddedCodeSmells),
                LogFields.Field("removed", refactor.RefactoringProperties?.RemovedCodeSmells));
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

        private static string SummarizeFnsRequest(FnsToRefactorRequestModel request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            string input = "none";
            int? smellCount = null;
            if (request is FnsToRefactorDeltaRequestModel delta && delta.DeltaResult != null)
            {
                input = "delta-result";
            }
            else if (request is FnsToRefactorCodeSmellRequestModel smells)
            {
                input = "code-smells";
                smellCount = smells.CodeSmells?.Count;
            }

            return LogFields.Format(
                LogFields.Field("fileName", request.FileName),
                LogFields.Field("input", input),
                LogFields.Field("smellCount", smellCount),
                LogFields.Field("hasCachePath", request.CachePath != null));
        }

        private static string SummarizeRefactorRequest(RefactorPostRequestModel request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            var format = request.FnToRefactorNippyB64 != null
                ? "nippy-b64"
                : request.FnToRefactor != null ? "json" : "none";
            return LogFields.Format(
                LogFields.Field("format", format),
                LogFields.Field("skipCache", request.SkipCache == true));
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

        private static string SummarizeFunction(FnToRefactorModel function)
        {
            if (function == null)
            {
                return "(invalid)";
            }

            var name = string.IsNullOrEmpty(function.Name) ? "(unnamed)" : function.Name;
            var range = function.Range == null
                ? string.Empty
                : "@" + function.Range.StartLine + ":" + function.Range.StartColumn + "-" + function.Range.EndLine + ":" + function.Range.EndColumn;
            var categories = function.RefactoringTargets == null
                ? string.Empty
                : string.Join(", ", function.RefactoringTargets.Select(target => target.Category).Where(category => !string.IsNullOrEmpty(category)));
            return name + range + (categories.Length == 0 ? string.Empty : "[" + categories + "]");
        }

        private static string Suffix(string summary)
        {
            return string.IsNullOrEmpty(summary) ? string.Empty : " " + summary;
        }
    }
}
