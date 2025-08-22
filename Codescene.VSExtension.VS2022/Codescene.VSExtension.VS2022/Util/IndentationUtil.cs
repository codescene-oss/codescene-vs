using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.Util
{
    public class IndentationUtil
    {
        public static int DetectIndentation(ITextSnapshot snapshot, FnToRefactorModel refactorableFunction)
        {
            // Get the line at the start of the function
            int startLine = Math.Max(0, refactorableFunction.Range.Startline - 1);
            if (startLine >= snapshot.LineCount)
                return 0;

            var line = snapshot.GetLineFromLineNumber(startLine);
            string lineText = line.GetText();

            // Count leading spaces
            int leadingSpaces = 0;
            while (leadingSpaces < lineText.Length && char.IsWhiteSpace(lineText[leadingSpaces]))
            {
                leadingSpaces++;
            }

            return leadingSpaces;
        }

        public static string AdjustIndentation(string code, int indentationLevel)
        {
            var indentation = new string(' ', indentationLevel); // 4 spaces per level
            var lines = code.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    lines[i] = indentation + lines[i];
                }
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
