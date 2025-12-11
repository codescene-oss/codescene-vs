using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.VS2022.Util
{
    public class IndentationUtil
    {
		/// <summary>
		/// Used to detect the indentation style (tabs vs spaces) and level of a given function in the text snapshot.
		/// </summary>
		public static IndentationInfo DetectIndentation(ITextSnapshot snapshot, FnToRefactorModel refactorableFunction)
        {
            int startLine = Math.Max(0, refactorableFunction.Range.Startline - 1);
            if (startLine >= snapshot.LineCount)
                return new IndentationInfo { Level = 0, UsesTabs = false, TabSize = 4 };

            var line = snapshot.GetLineFromLineNumber(startLine);
            string lineText = line.GetText();

            // Analyze the indentation pattern
            var indentationAnalysis = AnalyzeIndentationPattern(snapshot, startLine);
            
            // Count leading whitespace for this specific line
            int leadingWhitespace = 0;
            while (leadingWhitespace < lineText.Length && char.IsWhiteSpace(lineText[leadingWhitespace]))
            {
                leadingWhitespace++;
            }

            // Calculate the indentation level based on the detected pattern
            int indentationLevel;
            if (indentationAnalysis.UsesTabs)
            {
                // Count tabs in the leading whitespace
                int tabCount = 0;
                for (int i = 0; i < leadingWhitespace && i < lineText.Length; i++)
                {
                    if (lineText[i] == '\t') tabCount++;
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
                TabSize = indentationAnalysis.TabSize 
            };
        }

		/// <summary>
		/// Used to adjust the indentation of the given code snippet returned from ACE servise.
		/// It applies the specified indentation level and style (tabs or spaces) to each line
		/// </summary>
		public static string AdjustIndentation(string code, IndentationInfo indentationInfo)
        {
            if (indentationInfo.Level == 0)
                return code;

            string indentationString;
            if (indentationInfo.UsesTabs)
            {
                indentationString = new string('\t', indentationInfo.Level);
            }
            else
            {
                indentationString = new string(' ', indentationInfo.Level * indentationInfo.TabSize);
            }

            var lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            // Adjust indentation for all non-empty lines
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    lines[i] = indentationString + lines[i];
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static IndentationPattern AnalyzeIndentationPattern(ITextSnapshot snapshot, int startLine)
        {
            if (startLine >= snapshot.LineCount)
                return DefaultIndentationPattern();

            var line = snapshot.GetLineFromLineNumber(startLine);
            string lineText = line.GetText();

            if (string.IsNullOrWhiteSpace(lineText))
                return DefaultIndentationPattern();

            var (tabCount, spaceCount) = CountLeadingWhitespace(lineText);

            bool usesTabs = tabCount > 0;
            int tabSize = DetermineTabSize(usesTabs, spaceCount);

            return new IndentationPattern { UsesTabs = usesTabs, TabSize = tabSize };
        }

        private static IndentationPattern DefaultIndentationPattern()
        {
            return new IndentationPattern { UsesTabs = false, TabSize = 4 };
        }

        private static (int tabCount, int spaceCount) CountLeadingWhitespace(string lineText)
        {
            int tabCount = 0;
            int spaceCount = 0;
            int i = 0;

            while (i < lineText.Length && char.IsWhiteSpace(lineText[i]))
            {
                if (lineText[i] == '\t')
                    tabCount++;
                else if (lineText[i] == ' ')
                    spaceCount++;
                i++;
            }

            return (tabCount, spaceCount);
        }

        private static int DetermineTabSize(bool usesTabs, int spaceCount)
        {
            int tabSize = 4; // Default tab size
            if (!usesTabs && spaceCount > 0)
            {
                var possibleTabSizes = new[] { 2, 4, 8 };
                foreach (var size in possibleTabSizes)
                {
                    if (spaceCount % size == 0)
                    {
                        tabSize = size;
                        break;
                    }
                }
            }
            return tabSize;
        }

        private struct IndentationPattern
        {
            public bool UsesTabs { get; set; }
            public int TabSize { get; set; }
        }
    }

    public struct IndentationInfo
    {
        public int Level { get; set; }
        public bool UsesTabs { get; set; }
        public int TabSize { get; set; }
    }
}
