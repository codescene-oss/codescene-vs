// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Text.RegularExpressions;

namespace Codescene.VSExtension.Core.Util
{
    internal static class SensitiveDataRedactor
    {
        private static readonly Regex SecretCliFlagRegex = new Regex(
            @"--token(?:\s*=\s*|\s+)(?:""[^""]*""|'[^']*'|\S+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex JsonTokenPropertyRegex = new Regex(
            @"""token""\s*:\s*""[^""]*""",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BearerTokenRegex = new Regex(
            @"Bearer\s+\S+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AuthorizationHeaderRegex = new Regex(
            @"Authorization\s*:\s*\S+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SensitiveQueryParamRegex = new Regex(
            @"([?&])(token|access_token|api_key|apikey|password|key)=([^&\s""']*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex WindowsPathRegex = new Regex(
            @"(?<![\w.:])[a-zA-Z]:\\(?:[^""\s<>|*?]+\\)*[^""\s<>|*?]*",
            RegexOptions.Compiled);

        private static readonly Regex UncPathRegex = new Regex(
            @"\\\\[^\s""']+",
            RegexOptions.Compiled);

        private static readonly Regex UnixPathRegex = new Regex(
            @"(?<![\w.:])/(?:home|Users|var|tmp|opt|private)(?:/[^\s""']+)+",
            RegexOptions.Compiled);

        private static readonly Lazy<string> UserProfilePath = new Lazy<string>(() =>
            NormalizeDirectoryPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));

        public static string RedactCliTokenArguments(string arguments)
        {
            if (string.IsNullOrEmpty(arguments))
            {
                return arguments;
            }

            return SecretCliFlagRegex.Replace(arguments, "--token ***");
        }

        public static string RedactSecrets(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var result = RedactCliTokenArguments(value);
            result = JsonTokenPropertyRegex.Replace(result, @"""token"":""***""");
            result = BearerTokenRegex.Replace(result, "Bearer ***");
            result = AuthorizationHeaderRegex.Replace(result, "Authorization: ***");
            result = SensitiveQueryParamRegex.Replace(result, "$1$2=***");
            return result;
        }

        public static string RedactPaths(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var result = value;
            var userProfile = UserProfilePath.Value;
            if (!string.IsNullOrEmpty(userProfile) &&
                result.IndexOf(userProfile, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result = ReplaceIgnoreCase(result, userProfile, "<user>");
            }

            result = UncPathRegex.Replace(result, "<path>");
            result = WindowsPathRegex.Replace(result, "<path>");
            result = UnixPathRegex.Replace(result, "<path>");
            return result;
        }

        public static string RedactForTelemetry(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return RedactPaths(RedactSecrets(value));
        }

        private static string NormalizeDirectoryPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            return path.TrimEnd('\\', '/');
        }

        private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(oldValue))
            {
                return source;
            }

            var index = 0;
            while ((index = source.IndexOf(oldValue, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                source = source.Substring(0, index) + newValue + source.Substring(index + oldValue.Length);
                index += newValue.Length;
            }

            return source;
        }
    }
}
