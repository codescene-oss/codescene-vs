// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Util
{
    public static class LogFields
    {
        public const int PathListLimit = 20;

        public static KeyValuePair<string, object> Field(string key, object value)
        {
            return new KeyValuePair<string, object>(key, value);
        }

        public static string Format(params KeyValuePair<string, object>[] fields)
        {
            if (fields == null || fields.Length == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>(fields.Length);
            foreach (var field in fields)
            {
                if (field.Value == null)
                {
                    continue;
                }

                parts.Add(field.Key + "=" + FormatValue(field.Value));
            }

            return string.Join(" ", parts);
        }

        public static string FormatCappedList(IReadOnlyList<string> items, int limit = PathListLimit)
        {
            if (items == null || items.Count == 0)
            {
                return "(none)";
            }

            if (items.Count <= limit)
            {
                return string.Join(", ", items);
            }

            var shown = new string[limit];
            for (var i = 0; i < limit; i++)
            {
                shown[i] = items[i];
            }

            return string.Join(", ", shown) + " omitted=" + (items.Count - limit);
        }

        private static string FormatValue(object value)
        {
            if (value is bool || value is byte || value is short || value is int || value is long
                || value is float || value is double || value is decimal)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (value is string text)
            {
                return text.IndexOf(' ') >= 0 ? JsonConvert.SerializeObject(text) : text;
            }

            if (value is IEnumerable enumerable)
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                {
                    items.Add(Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty);
                }

                return FormatCappedList(items);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }
}
