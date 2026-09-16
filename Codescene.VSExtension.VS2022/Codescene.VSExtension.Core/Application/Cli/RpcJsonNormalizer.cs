// Copyright (c) CodeScene. All rights reserved.

using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Cli
{
    public static class RpcJsonNormalizer
    {
        public static JToken ToKebabCase(JToken token)
        {
            if (token is JObject obj)
            {
                var result = new JObject();
                foreach (var property in obj.Properties())
                {
                    result[ToKebabCaseName(property.Name)] = ToKebabCase(property.Value);
                }

                return result;
            }

            if (token is JArray array)
            {
                return new JArray(array.Select(ToKebabCase));
            }

            return token;
        }

        public static string ToKebabCaseName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            if (string.Equals(name, "nippyB64", System.StringComparison.Ordinal) ||
                string.Equals(name, "nippy-b-64", System.StringComparison.Ordinal))
            {
                return "nippy-b64";
            }

            if (name.IndexOf('-') >= 0)
            {
                return name;
            }

            var builder = new StringBuilder(name.Length + 8);
            for (var i = 0; i < name.Length; i++)
            {
                var current = name[i];
                if (char.IsUpper(current))
                {
                    if (i > 0)
                    {
                        builder.Append('-');
                    }

                    builder.Append(char.ToLowerInvariant(current));
                }
                else
                {
                    builder.Append(current);
                }
            }

            return builder.ToString();
        }
    }
}
