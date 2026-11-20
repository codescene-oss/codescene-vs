// Copyright (c) CodeScene. All rights reserved.

using System.Linq;
using Codescene.VSExtension.Core.Consts;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public static class AuthStatusFields
    {
        public static string State(JObject status)
        {
            return status?["state"]?.Value<string>();
        }

        public static string CredentialSource(JObject status)
        {
            return status?["credential-source"]?.Value<string>();
        }

        public static bool EnvTokenPresent(JObject status)
        {
            return status?["env-token-present"]?.Value<bool>() ?? false;
        }

        public static bool? OAuthSupported(JObject status)
        {
            var token = status?["oauth-supported"];
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }

            return token.Value<bool>();
        }

        public static bool ReauthorizeNeeded(JObject status)
        {
            return status?["reauthorize-needed"]?.Value<bool>() ?? false;
        }

        public static string LoginId(JObject status)
        {
            var login = status?["login"] as JObject;
            return login?["login-id"]?.Value<string>();
        }

        public static string AuthorizeUrl(JObject status)
        {
            var login = status?["login"] as JObject;
            return login?["authorize-url"]?.Value<string>();
        }

        public static string PatUrl(JObject status)
        {
            var server = status?["server"] as JObject;
            return server?["pat-url"]?.Value<string>();
        }

        public static string ErrorCode(JObject status)
        {
            var error = status?["error"] as JObject;
            return error?["code"]?.Value<string>();
        }

        public static string AccountDataKey(JObject status)
        {
            if (State(status) != AuthConstants.States.SIGNEDIN)
            {
                return null;
            }

            var serverUrl = status?["server"]?["url"]?.Value<string>();
            var credentialSource = CredentialSource(status);
            var userId = status?["user"]?["id"];
            var account = status?["account"];
            var accountId = (account != null && account.Type != JTokenType.Null) ? account["id"] : null;

            return string.Join("|", new object[] { serverUrl, credentialSource, userId, accountId }.Select(v => v?.ToString() ?? string.Empty));
        }
    }
}
