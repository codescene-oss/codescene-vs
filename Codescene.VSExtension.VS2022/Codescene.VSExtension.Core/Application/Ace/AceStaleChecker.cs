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
        /// True if the function was found at a different location.
        /// When true, <see cref="UpdatedRange"/> contains the new range.
        /// </summary>
        public bool RangeUpdated { get; set; }

        /// <summary>
        /// The updated range when the function was found at a different location.
        /// Only set when <see cref="RangeUpdated"/> is true.
        /// </summary>
        public CliRangeModel UpdatedRange { get; set; }
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
        /// This method does not mutate the input model; if the range changed,
        /// the new range is returned in <see cref="StaleCheckResult.UpdatedRange"/>.
        /// </summary>
        /// <param name="documentContent">The current content of the document.</param>
        /// <param name="fnToRefactor">The function being refactored with its original body and range.</param>
        /// <returns>A result indicating if the function is stale or if its range was updated (with the new range).</returns>
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
                // Calculate the new range without mutating the original model
                var newRange = CalculateNewRange(documentContent, fnToRefactor.Body, indexOfBody);
                return new StaleCheckResult { IsStale = false, RangeUpdated = true, UpdatedRange = newRange };
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
            var newlineSequence = DetectNewlineSequence(content);
            var context = CreateExtractionContext(range, lines, newlineSequence);

            if (!context.IsValid)
                return string.Empty;

            if (context.IsSingleLine)
                return ExtractSingleLineContent(context);

            return ExtractMultiLineContent(context);
        }

        /// <summary>
        /// Detects the newline sequence used in the content.
        /// Returns "\r\n" for Windows, "\r" for old Mac, "\n" for Unix, or "\n" as default.
        /// </summary>
        private string DetectNewlineSequence(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "\n";

            var crlfIndex = content.IndexOf("\r\n", StringComparison.Ordinal);
            var lfIndex = content.IndexOf("\n", StringComparison.Ordinal);
            var crIndex = content.IndexOf("\r", StringComparison.Ordinal);

            // Check for CRLF first (Windows) - it must appear before or at the same position as LF
            var hasCrlf = crlfIndex >= 0;
            var crlfAppearsFirst = lfIndex < 0 || crlfIndex <= lfIndex;
            if (hasCrlf && crlfAppearsFirst)
                return "\r\n";

            // Check for standalone LF (Unix)
            if (lfIndex >= 0)
                return "\n";

            // Check for standalone CR (old Mac)
            if (crIndex >= 0)
                return "\r";

            return "\n";
        }

        private ExtractionContext CreateExtractionContext(CliRangeModel range, string[] lines, string newlineSequence)
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
                IsSingleLine = startLine == endLine,
                NewlineSequence = newlineSequence
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
            var newline = ctx.NewlineSequence ?? "\n";

            // First line: from start column to end
            var firstStartCol = Math.Max(0, ctx.StartColumn - 1);
            if (firstStartCol < ctx.Lines[ctx.StartLine].Length)
                result.Append(ctx.Lines[ctx.StartLine].Substring(firstStartCol));

            // Middle lines: full lines
            for (int i = ctx.StartLine + 1; i < ctx.EndLine; i++)
            {
                result.Append(newline);
                result.Append(ctx.Lines[i]);
            }

            // Last line: from start to end column
            if (ctx.EndLine > ctx.StartLine && ctx.EndLine < ctx.Lines.Length)
            {
                result.Append(newline);
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
            public string NewlineSequence;
        }

        /// <summary>
        /// Calculates a new range pointing to the position where the body was found.
        /// Returns a new <see cref="CliRangeModel"/> without mutating any input.
        /// </summary>
        /// <param name="content">The document content.</param>
        /// <param name="body">The function body text.</param>
        /// <param name="newIndex">The character index where the body was found.</param>
        /// <returns>A new range representing the function's new position.</returns>
        private CliRangeModel CalculateNewRange(string content, string body, int newIndex)
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

                // Account for newline character(s) - detect actual separator length
                var separatorLength = GetNewlineLengthAtPosition(content, lineEndIndex);
                currentIndex = lineEndIndex + separatorLength;
            }

            // Calculate new end position based on body length
            var bodyLines = body.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var newEndLine = newStartLine + bodyLines.Length - 1;
            var newEndColumn = bodyLines.Length == 1
                ? newStartColumn + body.Length - 1
                : bodyLines[bodyLines.Length - 1].Length;

            return new CliRangeModel
            {
                Startline = newStartLine,
                StartColumn = newStartColumn,
                EndLine = newEndLine,
                EndColumn = newEndColumn
            };
        }

        /// <summary>
        /// Gets the length of the newline sequence at the specified position in the content.
        /// Returns 2 for CRLF, 1 for CR or LF, 0 if at EOF or no newline.
        /// </summary>
        private int GetNewlineLengthAtPosition(string content, int position)
        {
            if (position >= content.Length)
                return 0;

            var currentChar = content[position];
            var hasNextChar = position + 1 < content.Length;

            // Check for CRLF (Windows)
            var isCrlfSequence = hasNextChar && currentChar == '\r' && content[position + 1] == '\n';
            if (isCrlfSequence)
                return 2;

            // Check for CR (old Mac) or LF (Unix)
            var isSingleNewline = currentChar == '\r' || currentChar == '\n';
            if (isSingleNewline)
                return 1;

            return 0;
        }
    }
}
