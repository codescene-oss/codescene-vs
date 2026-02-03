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
    }
}
