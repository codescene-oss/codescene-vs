// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Codescene.VSExtension.Core.Application.Cli
{
    public sealed partial class IdeServerClient
    {
        public Task<JObject> AuthConfigureAsync(string serverUrl, string accessToken, CancellationToken cancellationToken = default)
        {
            var parameters = new JObject
            {
                ["server-url"] = serverUrl,
                ["access-token"] = accessToken,
            };
            return SendRequestAsync<JObject>("cs-ide/auth-configure", parameters, cancellationToken);
        }

        public Task<JObject> AuthStatusAsync(CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<JObject>("cs-ide/auth-status", new JObject(), cancellationToken);
        }

        public Task<JObject> AuthLoginAsync(CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<JObject>("cs-ide/auth-login", new JObject(), cancellationToken);
        }

        public Task<JObject> AuthReauthorizeAsync(CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<JObject>("cs-ide/auth-reauthorize", new JObject(), cancellationToken);
        }

        public Task<JObject> AuthLogoutAsync(CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<JObject>("cs-ide/auth-logout", new JObject(), cancellationToken);
        }

        public Task<JObject> AuthCancelLoginAsync(string loginId, CancellationToken cancellationToken = default)
        {
            var parameters = new JObject
            {
                ["login-id"] = loginId,
            };
            return SendRequestAsync<JObject>("cs-ide/auth-cancel-login", parameters, cancellationToken);
        }

        public Task<JArray> AuthListAccountsAsync(CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<JArray>("cs-ide/auth-list-accounts", new JObject(), cancellationToken);
        }

        public Task<JObject> AuthSelectAccountAsync(JToken accountId, CancellationToken cancellationToken = default)
        {
            var parameters = new JObject
            {
                ["account-id"] = accountId,
            };
            return SendRequestAsync<JObject>("cs-ide/auth-select-account", parameters, cancellationToken);
        }

        public Task<JObject> AuthSyncAccountAsync(string repoPath, CancellationToken cancellationToken = default)
        {
            var parameters = new JObject
            {
                ["repo-path"] = repoPath,
            };
            return SendRequestAsync<JObject>("cs-ide/auth-sync-account", parameters, cancellationToken);
        }

        public Task<JObject> AuthTokenAsync(CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<JObject>("cs-ide/auth-token", new JObject(), cancellationToken);
        }

        public Task<JArray> ProjectsListAsync(CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<JArray>("cs-ide/projects-list", new JObject(), cancellationToken);
        }

        public Task<JObject> ProjectLinkGetAsync(string repoPath, CancellationToken cancellationToken = default)
        {
            var parameters = new JObject
            {
                ["repo-path"] = repoPath,
            };
            return SendRequestAsync<JObject>("cs-ide/project-link-get", parameters, cancellationToken);
        }

        public Task<JObject> ProjectLinkSetAsync(string repoPath, JToken projectId, CancellationToken cancellationToken = default)
        {
            var parameters = new JObject
            {
                ["repo-path"] = repoPath,
                ["project-id"] = projectId ?? JValue.CreateNull(),
            };
            return SendRequestAsync<JObject>("cs-ide/project-link-set", parameters, cancellationToken);
        }

        private static DevtoolsException ToDevtoolsException(RemoteInvocationException ex)
        {
            var status = HttpStatusOrErrorCode(ex);
            var authErrorCode = AuthErrorCodeOf(ex);
            return authErrorCode != null
                ? new DevtoolsException(ex.Message, status, null, authErrorCode)
                : new DevtoolsException(ex.Message, status, null);
        }

        private static int HttpStatusOrErrorCode(RemoteInvocationException ex)
        {
            var match = Regex.Match(ex.Message ?? string.Empty, @"^Request failed \[(\d{3})\]");
            return match.Success && int.TryParse(match.Groups[1].Value, out var httpStatus) ? httpStatus : ex.ErrorCode;
        }

        private static string AuthErrorCodeOf(RemoteInvocationException ex)
        {
            if (ex.ErrorCode != -32803 || !(ex.ErrorData is JToken errorData))
            {
                return null;
            }

            var code = errorData["code"];
            return code != null && code.Type == JTokenType.String ? code.Value<string>() : null;
        }

        private static DevtoolsException ToDevtoolsException(RemoteMethodNotFoundException ex)
        {
            return new DevtoolsException(ex.Message, -32601, null);
        }

        private void HandleAuthStatusChanged(JObject notification)
        {
            if (notification == null)
            {
                return;
            }

            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(notification);
            var state = (string)normalized["state"];
            var credentialSource = (string)normalized["credential-source"];
            _logger.Info("[cs-ide] received authStatusChanged " + LogFields.Format(
                LogFields.Field("state", state),
                LogFields.Field("credentialSource", credentialSource)));
            AuthStatusChanged?.Invoke(this, normalized);
        }
    }
}
