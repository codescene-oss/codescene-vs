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
            return NormalizeLineEndings(input, Environment.NewLine);
        }

        /// <summary>
        /// Replaces every line ending in <paramref name="input"/> with <paramref name="lineEnding"/>.
        /// </summary>
        public static string NormalizeLineEndings(string input, string lineEnding)
        {
            if (input == null)
            {
                return null;
            }

            var ending = string.IsNullOrEmpty(lineEnding) ? Environment.NewLine : lineEnding;
            return Regex.Replace(input, @"\r\n|\r|\n", ending);
        }

        public static string DominantLineEnding(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Environment.NewLine;
            }

            CountLineEndings(text, out var crlf, out var lf, out var cr);
            return ChooseLineEnding(crlf, lf, cr);
        }

        public static string TrimForLogging(string value, int maxLength = 120)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength) + "...";
        }

        public static string RedactSensitiveCliArguments(string arguments) =>
            SensitiveDataRedactor.RedactCliTokenArguments(arguments);

        public static string BuildCommandForLogging(string arguments, string jsonContent, int maxValueLength = 120)
        {
            var safeArguments = RedactSensitiveCliArguments(arguments);

            if (string.IsNullOrEmpty(jsonContent))
            {
                return TrimForLogging(safeArguments, maxValueLength);
            }

            var entries = ExtractLoggableJsonEntries(jsonContent, maxValueLength);
            if (string.IsNullOrEmpty(entries))
            {
                return TrimForLogging(safeArguments, maxValueLength);
            }

            return $"{safeArguments} {entries}";
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

        private static void CountLineEndings(string text, out int crlf, out int lf, out int cr)
        {
            crlf = 0;
            lf = 0;
            cr = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (IsCrlfAt(text, i))
                {
                    crlf++;
                    i++;
                    continue;
                }

                if (text[i] == '\n')
                {
                    lf++;
                    continue;
                }

                if (text[i] == '\r')
                {
                    cr++;
                }
            }
        }

        private static bool IsCrlfAt(string text, int index)
        {
            if (text[index] != '\r' || index + 1 >= text.Length)
            {
                return false;
            }

            return text[index + 1] == '\n';
        }

        private static string ChooseLineEnding(int crlf, int lf, int cr)
        {
            if (PrefersCrlf(crlf, lf, cr))
            {
                return "\r\n";
            }

            if (lf > 0 && lf >= cr)
            {
                return "\n";
            }

            if (cr > 0)
            {
                return "\r";
            }

            return Environment.NewLine;
        }

        private static bool PrefersCrlf(int crlf, int lf, int cr)
        {
            if (crlf == 0)
            {
                return false;
            }

            return crlf >= lf && crlf >= cr;
        }

        private static bool ShouldSkipLoggableJsonProperty(string propertyName) =>
            propertyName == "file-content";

        private static string FormatLoggableJsonProperty(JProperty property, int maxValueLength)
        {
            if (property.Name == "token")
            {
                return "'token' \"***\"";
            }

            var value = property.Value.Type == JTokenType.String
                ? property.Value.ToString()
                : property.Value.ToString(Formatting.None);

            return $"'{property.Name}' \"{TrimForLogging(value, maxValueLength)}\"";
        }

        private static void AppendLoggableEntry(StringBuilder result, string entry)
        {
            if (result.Length > 0)
            {
                result.Append(" ");
            }

            result.Append(entry);
        }

        private static string FormatJsonProperties(JObject jsonObject, int maxValueLength)
        {
            try
            {
                var result = new StringBuilder();
                foreach (var property in jsonObject.Properties())
                {
                    if (ShouldSkipLoggableJsonProperty(property.Name))
                    {
                        continue;
                    }

                    AppendLoggableEntry(result, FormatLoggableJsonProperty(property, maxValueLength));
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
