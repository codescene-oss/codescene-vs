using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.Core.Interfaces.Util
{
    public interface IIndentationService
    {
        /// <summary>
        /// Adjusts the indentation of the given code snippet.
        /// Applies the specified indentation level and style (tabs or spaces) to each line.
        /// </summary>
        string AdjustIndentation(string code, IndentationInfo indentationInfo);

        /// <summary>
        /// Counts leading whitespace characters in a line.
        /// </summary>
        (int tabCount, int spaceCount) CountLeadingWhitespace(string lineText);

        /// <summary>
        /// Determines the tab size based on the indentation pattern.
        /// </summary>
        int DetermineTabSize(bool usesTabs, int spaceCount);
    }
}
