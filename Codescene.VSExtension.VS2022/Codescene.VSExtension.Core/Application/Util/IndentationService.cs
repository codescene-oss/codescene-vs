// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces.Util;
using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.Core.Application.Util
{
    [Export(typeof(IIndentationService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class IndentationService : IIndentationService
    {
        /// <summary>
        /// Adjusts the indentation of the given code snippet.
        /// Applies the specified indentation level and style (tabs or spaces) to each line.
        /// </summary>
        public string AdjustIndentation(string code, IndentationInfo indentationInfo)
        {
            if (indentationInfo.Level == 0)
            {
                return code;
            }

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

        /// <summary>
        /// Counts leading whitespace characters in a line.
        /// </summary>
        public (int tabCount, int spaceCount) CountLeadingWhitespace(string lineText)
        {
            int tabCount = 0;
            int spaceCount = 0;
            int i = 0;

            while (i < lineText.Length && char.IsWhiteSpace(lineText[i]))
            {
                if (lineText[i] == '\t')
                {
                    tabCount++;
                }
                else if (lineText[i] == ' ')
                {
                    spaceCount++;
                }

                i++;
            }

            return (tabCount, spaceCount);
        }

        /// <summary>
        /// Determines the tab size based on the indentation pattern.
        /// </summary>
        public int DetermineTabSize(bool usesTabs, int spaceCount)
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
    }
}
