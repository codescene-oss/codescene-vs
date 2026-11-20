// Copyright (c) CodeScene. All rights reserved.

using System.Collections.Generic;
using Codescene.VSExtension.Core.Consts;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public static class AuthTelemetryEvents
    {
        public static KeyValuePair<string, Dictionary<string, object>> SignInFailed(string code)
        {
            return new KeyValuePair<string, Dictionary<string, object>>(
                Constants.Telemetry.AUTHSIGNINFAILED,
                new Dictionary<string, object> { { "error-code", code ?? "unknown" } });
        }

        public static KeyValuePair<string, Dictionary<string, object>> ReauthorizeFailed(string code)
        {
            return new KeyValuePair<string, Dictionary<string, object>>(
                Constants.Telemetry.AUTHREAUTHORIZEFAILED,
                new Dictionary<string, object> { { "error-code", code ?? "unknown" } });
        }

        public static KeyValuePair<string, Dictionary<string, object>>? SignInOutcome(JObject previousStatus, JObject nextStatus)
        {
            var previousState = AuthStatusFields.State(previousStatus);
            var nextState = AuthStatusFields.State(nextStatus);

            if (previousState != AuthConstants.States.SIGNINGIN || nextState == AuthConstants.States.SIGNINGIN)
            {
                return null;
            }

            var errorCode = AuthStatusFields.ErrorCode(nextStatus);

            if (errorCode == "auth/cancelled")
            {
                return new KeyValuePair<string, Dictionary<string, object>>(
                    Constants.Telemetry.AUTHSIGNINCANCELLED,
                    null);
            }

            if (errorCode != null || nextState != AuthConstants.States.SIGNEDIN)
            {
                return SignInFailed(errorCode);
            }

            return new KeyValuePair<string, Dictionary<string, object>>(
                Constants.Telemetry.AUTHSIGNINCOMPLETED,
                null);
        }
    }
}
