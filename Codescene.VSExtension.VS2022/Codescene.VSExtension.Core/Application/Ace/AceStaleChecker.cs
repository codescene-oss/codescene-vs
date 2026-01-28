using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.Application.Ace
{
    /// <summary>
    /// Result of checking if a function has become stale in the document.
    /// </summary>
    public class StaleCheckResult
    {
        /// <summary>
        /// True if the function body can no longer be found in the document.
        /// </summary>
        public bool IsStale { get; set; }

        /// <summary>
        /// True if the function was found at a different location and the range was updated.
        /// </summary>
        public bool RangeUpdated { get; set; }
    }

    /// <summary>
    /// Checks if a function being refactored has become stale due to document changes.
    /// Following VSCode's isFunctionUnchangedInDocument logic.
    /// </summary>
    [Export(typeof(AceStaleChecker))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AceStaleChecker
    {
        /// <summary>
        /// Checks if the function body has changed in the document.
        /// </summary>
        /// <param name="documentContent">The current content of the document.</param>
        /// <param name="fnToRefactor">The function being refactored with its original body and range.</param>
        /// <returns>A result indicating if the function is stale or if its range was updated.</returns>
        public StaleCheckResult IsFunctionUnchangedInDocument(
            string documentContent,
            FnToRefactorModel fnToRefactor)
        {
            if (fnToRefactor?.Body == null || string.IsNullOrEmpty(documentContent))
                return new StaleCheckResult { IsStale = false, RangeUpdated = false };

            // Get content at the stored range
            var contentAtRange = GetContentAtRange(documentContent, fnToRefactor.Range);

            // If content matches what's stored, function is not stale
            if (contentAtRange == fnToRefactor.Body)
                return new StaleCheckResult { IsStale = false, RangeUpdated = false };

            // Function body doesn't match at range - search elsewhere in document
            var indexOfBody = documentContent.IndexOf(fnToRefactor.Body, StringComparison.Ordinal);
            if (indexOfBody >= 0)
            {
                // Body exists somewhere else (function was moved)
                // Update the range to the new position
                UpdateRangeToNewPosition(documentContent, fnToRefactor, indexOfBody);
                return new StaleCheckResult { IsStale = false, RangeUpdated = true };
            }

            // Body not found anywhere - function is stale
            return new StaleCheckResult { IsStale = true, RangeUpdated = false };
        }

        /// <summary>
        /// Extracts the text content at the specified range from the document.
        /// </summary>
        private string GetContentAtRange(string content, CliRangeModel range)
        {
            if (range == null)
                return string.Empty;

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var context = CreateExtractionContext(range, lines);

            if (!context.IsValid)
                return string.Empty;

            if (context.IsSingleLine)
                return ExtractSingleLineContent(context);

            return ExtractMultiLineContent(context);
        }

        private ExtractionContext CreateExtractionContext(CliRangeModel range, string[] lines)
        {
            // Range is 1-indexed, convert to 0-indexed
            var startLine = range.Startline - 1;
            var endLine = range.EndLine - 1;

            var isValid = startLine >= 0 && startLine < lines.Length && startLine <= endLine;
            if (!isValid)
                return new ExtractionContext { IsValid = false };

            if (endLine >= lines.Length)
                endLine = lines.Length - 1;

            return new ExtractionContext
            {
                Lines = lines,
                StartLine = startLine,
                EndLine = endLine,
                StartColumn = range.StartColumn,
                EndColumn = range.EndColumn,
                IsValid = true,
                IsSingleLine = startLine == endLine
            };
        }

        private string ExtractSingleLineContent(ExtractionContext ctx)
        {
            var line = ctx.Lines[ctx.StartLine];
            var startCol = Math.Max(0, ctx.StartColumn - 1);
            var endCol = Math.Min(line.Length, ctx.EndColumn);

            if (startCol >= line.Length)
                return string.Empty;

            return line.Substring(startCol, Math.Max(0, endCol - startCol));
        }

        private string ExtractMultiLineContent(ExtractionContext ctx)
        {
            var result = new System.Text.StringBuilder();

            // First line: from start column to end
            var firstStartCol = Math.Max(0, ctx.StartColumn - 1);
            if (firstStartCol < ctx.Lines[ctx.StartLine].Length)
                result.Append(ctx.Lines[ctx.StartLine].Substring(firstStartCol));

            // Middle lines: full lines
            for (int i = ctx.StartLine + 1; i < ctx.EndLine; i++)
            {
                result.Append("\n");
                result.Append(ctx.Lines[i]);
            }

            // Last line: from start to end column
            if (ctx.EndLine > ctx.StartLine && ctx.EndLine < ctx.Lines.Length)
            {
                result.Append("\n");
                var lastEndCol = Math.Min(ctx.Lines[ctx.EndLine].Length, ctx.EndColumn);
                result.Append(ctx.Lines[ctx.EndLine].Substring(0, lastEndCol));
            }

            return result.ToString();
        }

        private struct ExtractionContext
        {
            public string[] Lines;
            public int StartLine;
            public int EndLine;
            public int StartColumn;
            public int EndColumn;
            public bool IsValid;
            public bool IsSingleLine;
        }

        /// <summary>
        /// Updates the function's range to point to the new position where the body was found.
        /// </summary>
        private void UpdateRangeToNewPosition(string content, FnToRefactorModel fn, int newIndex)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Find line and column for the new index
            int currentIndex = 0;
            int newStartLine = 0;
            int newStartColumn = 0;

            for (int lineNum = 0; lineNum < lines.Length; lineNum++)
            {
                var lineLength = lines[lineNum].Length;
                var lineEndIndex = currentIndex + lineLength;

                if (newIndex <= lineEndIndex)
                {
                    newStartLine = lineNum + 1; // Convert to 1-indexed
                    newStartColumn = newIndex - currentIndex + 1; // Convert to 1-indexed
                    break;
                }

                // Account for newline character
                currentIndex = lineEndIndex + 1;
            }

            // Calculate new end position based on body length
            var bodyLines = fn.Body.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var newEndLine = newStartLine + bodyLines.Length - 1;
            var newEndColumn = bodyLines.Length == 1
                ? newStartColumn + fn.Body.Length - 1
                : bodyLines[bodyLines.Length - 1].Length;

            // Update the range
            fn.Range.Startline = newStartLine;
            fn.Range.StartColumn = newStartColumn;
            fn.Range.EndLine = newEndLine;
            fn.Range.EndColumn = newEndColumn;
        }
    }
}
