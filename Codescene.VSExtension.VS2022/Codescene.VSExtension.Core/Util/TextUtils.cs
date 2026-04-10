// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Util
{
    public static class TextUtils
    {
        public static string ToSnakeCase(string input)
        {
            var normalized = input.Replace("-", "_");
            var cleaned = Regex.Replace(normalized, @"[^\w\s]", string.Empty);

            return string.Join(
                "_",
                cleaned
                    .Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                    .Select(word => word.ToLowerInvariant()));
        }

        /// <summary>
        /// Replaces all line ending characters in the specified string with the platform-specific line ending sequence.
        /// </summary>
        /// <remarks>This method replaces all occurrences of '\r', '\n', and '\r\n' with the value of <see
        /// cref="Environment.NewLine"/>, ensuring consistent line endings across platforms.</remarks>
        /// <param name="input">The input string in which to normalize line endings. Can be null or empty.</param>
        /// <returns>A string with all line endings replaced by <see cref="Environment.NewLine"/>. If <paramref name="input"/> is
        /// null, returns null.</returns>
        public static string NormalizeLineEndings(string input)
        {
            if (input == null)
            {
                return null;
            }

            return Regex.Replace(input, @"\r\n|\r|\n", Environment.NewLine);
        }

        public static string TrimForLogging(string value, int maxLength = 120)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
        }

        public static string BuildCommandForLogging(string arguments, string jsonContent, int maxValueLength = 120)
        {
            if (string.IsNullOrEmpty(jsonContent))
            {
                return TrimForLogging(arguments, maxValueLength);
            }

            var entries = ExtractLoggableJsonEntries(jsonContent, maxValueLength);
            if (string.IsNullOrEmpty(entries))
            {
                return TrimForLogging(arguments, maxValueLength);
            }

            return $"{arguments} {entries}";
        }

        public static string ExtractLoggableJsonEntries(string jsonContent, int maxValueLength = 120)
        {
            if (string.IsNullOrEmpty(jsonContent))
            {
                return string.Empty;
            }

            JObject jsonObject;
            try
            {
                jsonObject = JsonConvert.DeserializeObject<JObject>(jsonContent);
            }
            catch (JsonException)
            {
                var plainText = jsonContent;
                return TrimForLogging(plainText, maxValueLength);
            }

            if (jsonObject == null)
            {
                return string.Empty;
            }

            return FormatJsonProperties(jsonObject, maxValueLength);
        }

        private static string FormatJsonProperties(JObject jsonObject, int maxValueLength)
        {
            try
            {
                var result = new StringBuilder();
                foreach (var property in jsonObject.Properties())
                {
                    if (property.Name == "file-content")
                    {
                        continue;
                    }

                    var value = property.Value.Type == JTokenType.String
                        ? property.Value.ToString()
                        : property.Value.ToString(Formatting.None);

                    var trimmedValue = TrimForLogging(value, maxValueLength);

                    if (result.Length > 0)
                    {
                        result.Append(" ");
                    }

                    result.Append($"'{property.Name}' \"{trimmedValue}\"");
                }

                return result.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
