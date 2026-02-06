// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Linq;
using System.Text.RegularExpressions;

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
    }
}
