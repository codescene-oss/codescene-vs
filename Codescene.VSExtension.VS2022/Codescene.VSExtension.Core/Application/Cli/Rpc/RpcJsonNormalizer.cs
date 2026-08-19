// Copyright (c) CodeScene. All rights reserved.

using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Cli.Rpc
{
    public static class RpcJsonNormalizer
    {
        public static T Deserialize<T>(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return default;
            }

            return Normalize(token).ToObject<T>();
        }

        public static JToken Normalize(JToken token)
        {
            if (token == null)
            {
                return null;
            }

            if (token is JObject obj)
            {
                var result = new JObject();
                foreach (var property in obj.Properties())
                {
                    result[ToKebabCase(property.Name)] = Normalize(property.Value);
                }

                return result;
            }

            if (token is JArray array)
            {
                return new JArray(array.Select(Normalize));
            }

            return token.DeepClone();
        }

        public static string ToKebabCase(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf('-') >= 0)
            {
                return name;
            }

            var builder = new StringBuilder(name.Length + 4);
            for (var i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                    {
                        builder.Append('-');
                    }

                    builder.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }
    }
}
