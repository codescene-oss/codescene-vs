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
                    SanitizeJObject((JObject)token);
                    break;
                case JTokenType.Array:
                    SanitizeJArray((JArray)token);
                    break;
            }
        }

        private static void SanitizeJArray(JArray array)
        {
            foreach (var child in array)
            {
                SanitizeToken(child);
            }
        }

        private static object SanitizeObject(object value)
        {
            if (value == null)
            {
                return null;
            }

            return value switch
            {
                string text => SanitizeString(text),
                Dictionary<string, object> dictionary => SanitizeDictionary(dictionary),
                IDictionary<string, object> genericDictionary => SanitizeDictionary(CopyDictionary(genericDictionary)),
                IEnumerable enumerable when value is not string => SanitizeEnumerable(enumerable),
                _ => value,
            };
        }

        private static Dictionary<string, object> CopyDictionary(IDictionary<string, object> source)
        {
            var copy = new Dictionary<string, object>(source.Count);
            foreach (var entry in source)
            {
                copy[entry.Key] = entry.Value;
            }

            return copy;
        }

        private static List<object> SanitizeEnumerable(IEnumerable source)
        {
            var list = new List<object>();
            foreach (var item in source)
            {
                list.Add(SanitizeObject(item));
            }

            return list;
        }
    }
}
