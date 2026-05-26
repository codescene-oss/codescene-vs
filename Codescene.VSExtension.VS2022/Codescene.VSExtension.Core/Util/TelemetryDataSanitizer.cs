// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Util
{
    public static class TelemetryDataSanitizer
    {
        public static string SanitizeString(string value) =>
            SensitiveDataRedactor.RedactForTelemetry(value);

        public static void SanitizeJObject(JObject jObject)
        {
            if (jObject == null)
            {
                return;
            }

            foreach (var property in jObject.Properties())
            {
                SanitizeToken(property.Value);
            }
        }

        public static Dictionary<string, object> SanitizeDictionary(Dictionary<string, object> source)
        {
            if (source == null)
            {
                return null;
            }

            var result = new Dictionary<string, object>(source.Count);
            foreach (var kvp in source)
            {
                result[kvp.Key] = SanitizeObject(kvp.Value);
            }

            return result;
        }

        private static void SanitizeToken(JToken token)
        {
            if (token == null)
            {
                return;
            }

            switch (token.Type)
            {
                case JTokenType.String:
                    token.Replace(SanitizeString(token.Value<string>()));
                    break;
                case JTokenType.Object:
                    foreach (var child in ((JObject)token).Properties())
                    {
                        SanitizeToken(child.Value);
                    }

                    break;
                case JTokenType.Array:
                    foreach (var child in (JArray)token)
                    {
                        SanitizeToken(child);
                    }

                    break;
            }
        }

        private static object SanitizeObject(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is string text)
            {
                return SanitizeString(text);
            }

            if (value is Dictionary<string, object> dictionary)
            {
                return SanitizeDictionary(dictionary);
            }

            if (value is IDictionary<string, object> genericDictionary)
            {
                var copy = new Dictionary<string, object>();
                foreach (var entry in genericDictionary)
                {
                    copy[entry.Key] = entry.Value;
                }

                return SanitizeDictionary(copy);
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(SanitizeObject(item));
                }

                return list;
            }

            return value;
        }
    }
}
