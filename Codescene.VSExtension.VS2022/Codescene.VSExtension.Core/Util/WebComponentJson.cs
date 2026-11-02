// Copyright (c) CodeScene. All rights reserved.

using Newtonsoft.Json;
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

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);
    }
}
