// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Cli
{
    internal static partial class IdeServerLog
    {
        private static readonly Dictionary<string, System.Func<object, string>> AuthRequestSummaries =
            new Dictionary<string, System.Func<object, string>>
            {
                { "cs-ide/auth-configure", SummarizeAuthConfigureRequest },
                { "cs-ide/auth-cancel-login", SummarizeCancelLoginRequest },
                { "cs-ide/auth-sync-account", SummarizeRepoPathRequest },
                { "cs-ide/project-link-get", SummarizeRepoPathRequest },
                { "cs-ide/project-link-set", SummarizeProjectLinkSetRequest },
            };

        private static readonly Dictionary<string, System.Func<object, string>> AuthResponseSummaries =
            new Dictionary<string, System.Func<object, string>>
            {
                { "cs-ide/auth-configure", SummarizeAuthStatusResponse },
                { "cs-ide/auth-status", SummarizeAuthStatusResponse },
                { "cs-ide/auth-login", SummarizeAuthStatusResponse },
                { "cs-ide/auth-reauthorize", SummarizeAuthStatusResponse },
                { "cs-ide/auth-logout", SummarizeAuthStatusResponse },
                { "cs-ide/auth-cancel-login", SummarizeAuthStatusResponse },
                { "cs-ide/auth-select-account", SummarizeAuthStatusResponse },
                { "cs-ide/auth-list-accounts", SummarizeListResponse },
                { "cs-ide/projects-list", SummarizeListResponse },
                { "cs-ide/project-link-get", SummarizeProjectLinkResponse },
                { "cs-ide/project-link-set", SummarizeProjectLinkResponse },
            };

        static IdeServerLog()
        {
            foreach (var entry in AuthRequestSummaries)
            {
                RequestSummaries[entry.Key] = entry.Value;
            }

            foreach (var entry in AuthResponseSummaries)
            {
                ResponseSummaries[entry.Key] = entry.Value;
            }
        }

        private static string SummarizeAuthConfigureRequest(object argument)
        {
            var parameters = argument as JObject;
            if (parameters == null)
            {
                return string.Empty;
            }

            var serverUrl = (string)parameters["server-url"];
            var accessToken = (string)parameters["access-token"];
            return LogFields.Format(
                LogFields.Field("serverUrl", serverUrl != null ? "set" : "unset"),
                LogFields.Field("tokenPresent", accessToken != null));
        }

        private static string SummarizeCancelLoginRequest(object argument)
        {
            return SummarizeParameters(argument, parameters => LogFields.Field("loginId", parameters["login-id"] != null ? "present" : "null"));
        }

        private static string SummarizeRepoPathRequest(object argument)
        {
            return SummarizeParameters(argument, parameters => LogFields.Field("repo", (string)parameters["repo-path"] ?? "(null)"));
        }

        private static string SummarizeParameters(object argument, Func<JObject, KeyValuePair<string, object>> field)
        {
            return argument is JObject parameters ? LogFields.Format(field(parameters)) : string.Empty;
        }

        private static string SummarizeProjectLinkSetRequest(object argument)
        {
            var parameters = argument as JObject;
            if (parameters == null)
            {
                return string.Empty;
            }

            var repoPath = (string)parameters["repo-path"];
            var projectId = parameters["project-id"];
            return LogFields.Format(
                LogFields.Field("repo", repoPath ?? "(null)"),
                LogFields.Field("projectId", projectId == null || projectId.Type == JTokenType.Null ? "null" : "set"));
        }

        private static string SummarizeAuthStatusResponse(object result)
        {
            var status = result as JObject;
            if (status == null)
            {
                return string.Empty;
            }

            if (status["login-id"] != null)
            {
                return LogFields.Format(LogFields.Field("loginPending", true));
            }

            var state = (string)status["state"];
            var credentialSource = (string)status["credential-source"] ?? (string)status["credentialSource"];
            var login = status["login"];

            if (login != null)
            {
                return LogFields.Format(
                    LogFields.Field("state", state),
                    LogFields.Field("credentialSource", credentialSource),
                    LogFields.Field("loginPending", true));
            }

            return LogFields.Format(
                LogFields.Field("state", state),
                LogFields.Field("credentialSource", credentialSource));
        }

        private static string SummarizeListResponse(object result)
        {
            var list = result as JArray;
            if (list == null)
            {
                return string.Empty;
            }

            return LogFields.Format(LogFields.Field("count", list.Count));
        }

        private static string SummarizeProjectLinkResponse(object result)
        {
            var link = result as JObject;
            if (link == null)
            {
                return string.Empty;
            }

            var project = link["project"];
            var source = (string)link["source"];
            var reason = (string)link["reason"];

            return LogFields.Format(
                LogFields.Field("linked", project != null && project.Type != JTokenType.Null),
                LogFields.Field("source", source),
                LogFields.Field("reason", reason));
        }
    }
}
