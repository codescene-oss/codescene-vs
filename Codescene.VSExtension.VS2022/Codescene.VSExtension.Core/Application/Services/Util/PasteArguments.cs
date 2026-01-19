// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("Codescene.VSExtension.Core.Tests")]
namespace Codescene.VSExtension.Core.Application.Services.Util
{
    internal static partial class PasteArguments
    {
        /// <summary>
        /// Used to escape special characters in CLI command argument. 
        /// </summary>
        internal static void AppendArgument(StringBuilder stringBuilder, string argument)
        {
            if (stringBuilder.Length != 0)
            {
                stringBuilder.Append(' ');
            }   

            // Parsing rules for non-argv[0] arguments:
            //   - Backslash is a normal character except followed by a quote.
            //   - 2N backslashes followed by a quote ==> N literal backslashes followed by unescaped quote
            //   - 2N+1 backslashes followed by a quote ==> N literal backslashes followed by a literal quote
            //   - Parsing stops at first whitespace outside of quoted region.
            //   - (post 2008 rule): A closing quote followed by another quote ==> literal quote, and parsing remains in quoting mode.
            if (argument.Length != 0 && ContainsNoWhitespaceOrQuotes(argument))
            {
                // Simple case - no quoting or changes needed.
                stringBuilder.Append(argument);
            }
            else
            {
                AppendArgumentWithQuotes(stringBuilder, argument);
            }
        }

        private static void AppendArgumentWithQuotes(StringBuilder stringBuilder, string argument)
        {
            stringBuilder.Append(Quote);
            int idx = 0;
            while (idx < argument.Length)
            {
                char c = argument[idx++];
                if (c == Backslash)
                {
                    HandleBackslashes(stringBuilder, argument, ref idx);
                }
                else if (c == Quote)
                {
                    EscapeQuote(stringBuilder);
                }
                else
                {
                    stringBuilder.Append(c);
                }
            }
            stringBuilder.Append(Quote);
        }

        private static void HandleBackslashes(StringBuilder stringBuilder, string argument, ref int idx)
        {
            int numBackSlash = 1;
            while (idx < argument.Length && argument[idx] == Backslash)
            {
                idx++;
                numBackSlash++;
            }

            if (idx == argument.Length)
            {
                // We'll emit an end quote after this so must double the number of backslashes.
                stringBuilder.Append(Backslash, numBackSlash * 2);
            }
            else if (argument[idx] == Quote)
            {
                // Backslashes will be followed by a quote. Must double the number of backslashes.
                stringBuilder.Append(Backslash, numBackSlash * 2 + 1);
                stringBuilder.Append(Quote);
                idx++;
            }
            else
            {
                // Backslash will not be followed by a quote, so emit as normal characters.
                stringBuilder.Append(Backslash, numBackSlash);
            }
        }

        private static void EscapeQuote(StringBuilder stringBuilder)
        {
            // Escape the quote so it appears as a literal. This also guarantees that we won't end up generating a closing quote followed
            // by another quote (which parses differently pre-2008 vs. post-2008.)
            stringBuilder.Append(Backslash);
            stringBuilder.Append(Quote);
        }

        private static bool ContainsNoWhitespaceOrQuotes(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (char.IsWhiteSpace(c) || c == Quote)
                {
                    return false;
                }
            }

            return true;
        }

        private const char Quote = '\"';
        private const char Backslash = '\\';
    }
}
