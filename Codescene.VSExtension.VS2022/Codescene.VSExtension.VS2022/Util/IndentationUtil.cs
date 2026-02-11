// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Models;
using Microsoft.VisualStudio.Text;

namespace Codescene.VSExtension.VS2022.Util
{
    public class IndentationUtil
    {
        private static readonly IndentationService IndentationService = new IndentationService();

        /// <summary>
        /// Detects the indentation style (tabs vs spaces) and level of a given function in the text snapshot.
        /// </summary>
        public static IndentationInfo DetectIndentation(ITextSnapshot snapshot, int fnStartLine)
        {
            var startLine = Math.Max(0, fnStartLine - 1);
            if (startLine >= snapshot.LineCount)
            {
                return new IndentationInfo { Level = 0, UsesTabs = false, TabSize = 4 };
            }

            var line = snapshot.GetLineFromLineNumber(startLine);
            var lineText = line.GetText();

            // Analyze the indentation pattern
            var indentationAnalysis = AnalyzeIndentationPattern(snapshot, startLine);

            // Count leading whitespace for this specific line
            var leadingWhitespace = 0;
            while (leadingWhitespace < lineText.Length && char.IsWhiteSpace(lineText[leadingWhitespace]))
            {
                leadingWhitespace++;
            }

            // Calculate the indentation level based on the detected pattern
            int indentationLevel;
            if (indentationAnalysis.UsesTabs)
            {
                // Count tabs in the leading whitespace
                var tabCount = 0;
                for (var i = 0; i < leadingWhitespace && i < lineText.Length; i++)
                {
                    if (lineText[i] == '\t')
                    {
                        tabCount++;
                    }
                }

                indentationLevel = tabCount;
            }
            else
            {
                // Calculate based on spaces and detected tab size
                indentationLevel = leadingWhitespace / indentationAnalysis.TabSize;
            }

            return new IndentationInfo
            {
                Level = indentationLevel,
                UsesTabs = indentationAnalysis.UsesTabs,
                TabSize = indentationAnalysis.TabSize,
            };
        }

        /// <summary>
        /// Adjusts the indentation of the given code snippet.
        /// </summary>
        public static string AdjustIndentation(string code, IndentationInfo indentationInfo)
        {
            return IndentationService.AdjustIndentation(code, indentationInfo);
        }

        private static IndentationPattern AnalyzeIndentationPattern(ITextSnapshot snapshot, int startLine)
        {
            if (startLine >= snapshot.LineCount)
            {
                return DefaultIndentationPattern();
            }

            var line = snapshot.GetLineFromLineNumber(startLine);
            var lineText = line.GetText();

            if (string.IsNullOrWhiteSpace(lineText))
            {
                return DefaultIndentationPattern();
            }

            var (tabCount, spaceCount) = IndentationService.CountLeadingWhitespace(lineText);

            var usesTabs = tabCount > 0;
            var tabSize = IndentationService.DetermineTabSize(usesTabs, spaceCount);

            return new IndentationPattern { UsesTabs = usesTabs, TabSize = tabSize };
        }

        private static IndentationPattern DefaultIndentationPattern()
        {
            return new IndentationPattern { UsesTabs = false, TabSize = 4 };
        }

        private struct IndentationPattern
        {
            public bool UsesTabs { get; set; }

            public int TabSize { get; set; }
        }
    }
}
