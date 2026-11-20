// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Application.Auth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Codescene.VSExtension.Core.Util
{
    /// <summary>
    /// The JSON the extension hands to the CWF webview: both the initial <c>window.ideContext</c> and the
    /// <c>update-renderer</c> messages. Property names are camelCase unless a model names them explicitly, and an
    /// optional value that is not there is left out: the webview types declare it optional, not nullable.
    /// </summary>
    public static class WebComponentJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new CamelCaseNamingStrategy() },
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None,
        };

        public static Func<AuthSnapshot> AuthSnapshotSource { get; set; }

        public static string Serialize(object value)
        {
            var authSnapshot = AuthSnapshotSource?.Invoke();
            return Serialize(value, authSnapshot ?? AuthSnapshot.Inactive);
        }

        public static string Serialize(object value, AuthSnapshot auth)
        {
            if (auth == null || !auth.Active)
            {
                return JsonConvert.SerializeObject(value, Settings);
            }

            var json = JObject.FromObject(value, JsonSerializer.CreateDefault(Settings));
            InjectAuth(json, auth);
            return json.ToString(Formatting.None);
        }

        private static void InjectAuth(JObject json, AuthSnapshot auth)
        {
            if (json["view"] != null || json["ideType"] != null)
            {
                AuthWebviewFields.Inject(json, auth);
                return;
            }

            if (json["messageType"]?.Value<string>() == "update-renderer")
            {
                var payload = json["payload"] as JObject;
                if (payload != null)
                {
                    AuthWebviewFields.Inject(payload, auth);
                }
            }
        }
    }
}
