using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Codescene.VSExtension.Core.Models.WebComponent.Util
{
    public static class TextUtils
    {
        public static string ToSnakeCase(string input)
        {
            var normalized = input.Replace("-", "_");
            var cleaned = Regex.Replace(normalized, @"[^\w\s]", "");

            return string.Join("_",
                cleaned
                    .Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                    .Select(word => word.ToLowerInvariant()));
        }
    }
}
