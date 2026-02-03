// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Models.Ace;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Application.Ace
{
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
            {
                return new StaleCheckResult { IsStale = false, RangeUpdated = false };
            }

            // Get content at the stored range
            var contentAtRange = GetContentAtRange(documentContent, fnToRefactor.Range);

            // Normalize newlines for comparison (LF/CRLF differences should not cause staleness)
            var normalizedContentAtRange = NormalizeNewlines(contentAtRange);
            var normalizedBody = NormalizeNewlines(fnToRefactor.Body);

            // If content matches what's stored, function is not stale
            if (normalizedContentAtRange == normalizedBody)
            {
                return new StaleCheckResult { IsStale = false, RangeUpdated = false };
            }

            // Function body doesn't match at range - search elsewhere in document
            // Normalize document content for IndexOf search
            var normalizedDocumentContent = NormalizeNewlines(documentContent);
            var indexOfBody = normalizedDocumentContent.IndexOf(normalizedBody, StringComparison.Ordinal);
            if (indexOfBody >= 0)
            {
                // Body exists somewhere else (function was moved)
                // Calculate the new range using normalized content for correct position mapping
                var newRange = CalculateNewRange(normalizedDocumentContent, normalizedBody, indexOfBody);
                return new StaleCheckResult { IsStale = false, RangeUpdated = true, UpdatedRange = newRange };
            }

            // Body not found anywhere - function is stale
            return new StaleCheckResult { IsStale = true, RangeUpdated = false };
        }

        /// <summary>
        /// Normalizes newlines by converting all line endings to LF (\n).
        /// This ensures consistent comparison regardless of CRLF vs LF differences.
        /// </summary>
        private static string NormalizeNewlines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        /// <summary>
        /// Extracts the text content at the specified range from the document.
        /// </summary>
        private string GetContentAtRange(string content, CliRangeModel range)
        {
            if (range == null)
            {
                return string.Empty;
            }

            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var newlineSequence = DetectNewlineSequence(content);
            var context = CreateExtractionContext(range, lines, newlineSequence);

            if (!context.IsValid)
            {
                return string.Empty;
            }

            if (context.IsSingleLine)
            {
                return ExtractSingleLineContent(context);
            }

            return ExtractMultiLineContent(context);
        }

        /// <summary>
        /// Detects the newline sequence used in the content.
        /// Returns "\r\n" for Windows, "\r" for old Mac, "\n" for Unix, or "\n" as default.
        /// </summary>
        private string DetectNewlineSequence(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return "\n";
            }

            var crlfIndex = content.IndexOf("\r\n", StringComparison.Ordinal);
            if (crlfIndex >= 0)
            {
                return "\r\n";
            }

            var lfIndex = content.IndexOf("\n", StringComparison.Ordinal);
            if (lfIndex >= 0)
            {
                return "\n";
            }

            var crIndex = content.IndexOf("\r", StringComparison.Ordinal);
            if (crIndex >= 0)
            {
                return "\r";
            }

            return "\n";
        }

        private ExtractionContext CreateExtractionContext(CliRangeModel range, string[] lines, string newlineSequence)
        {
            // Range is 1-indexed, convert to 0-indexed
            var startLine = range.StartLine - 1;
            var endLine = range.EndLine - 1;

            if (!IsValidRange(startLine, endLine, lines.Length))
            {
                return new ExtractionContext { IsValid = false };
            }

            var clampedEndLine = Math.Min(endLine, lines.Length - 1);

            return new ExtractionContext
            {
                Lines = lines,
                StartLine = startLine,
                EndLine = clampedEndLine,
                StartColumn = range.StartColumn,
                EndColumn = range.EndColumn,
                IsValid = true,
                IsSingleLine = startLine == clampedEndLine,
                NewlineSequence = newlineSequence,
            };
        }

        private static bool IsValidRange(int startLine, int endLine, int lineCount)
        {
            return startLine >= 0 && startLine < lineCount && startLine <= endLine;
        }

        private string ExtractSingleLineContent(ExtractionContext ctx)
        {
            var line = ctx.Lines[ctx.StartLine];
            var startCol = Math.Max(0, ctx.StartColumn - 1);
            var endCol = Math.Min(line.Length, ctx.EndColumn);

            if (startCol >= line.Length)
            {
                return string.Empty;
            }

            return line.Substring(startCol, Math.Max(0, endCol - startCol));
        }

        private string ExtractMultiLineContent(ExtractionContext ctx)
        {
            var result = new System.Text.StringBuilder();
            var newline = ctx.NewlineSequence ?? "\n";

            AppendFirstLine(result, ctx);
            AppendMiddleLines(result, ctx, newline);
            AppendLastLine(result, ctx, newline);

            return result.ToString();
        }

        private static void AppendFirstLine(System.Text.StringBuilder result, ExtractionContext ctx)
        {
            var firstStartCol = Math.Max(0, ctx.StartColumn - 1);
            if (firstStartCol < ctx.Lines[ctx.StartLine].Length)
            {
                result.Append(ctx.Lines[ctx.StartLine].Substring(firstStartCol));
            }
        }

        private static void AppendMiddleLines(System.Text.StringBuilder result, ExtractionContext ctx, string newline)
        {
            for (int i = ctx.StartLine + 1; i < ctx.EndLine; i++)
            {
                result.Append(newline);
                result.Append(ctx.Lines[i]);
            }
        }

        private static void AppendLastLine(System.Text.StringBuilder result, ExtractionContext ctx, string newline)
        {
            var hasLastLine = ctx.EndLine > ctx.StartLine && ctx.EndLine < ctx.Lines.Length;
            if (!hasLastLine)
            {
                return;
            }

            result.Append(newline);
            var lastEndCol = Math.Min(ctx.Lines[ctx.EndLine].Length, ctx.EndColumn);
            result.Append(ctx.Lines[ctx.EndLine].Substring(0, lastEndCol));
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
            var startPosition = FindLineAndColumn(content, lines, newIndex);
            var endPosition = CalculateEndPosition(body, startPosition);

            return new CliRangeModel
            {
                StartLine = startPosition.Line,
                StartColumn = startPosition.Column,
                EndLine = endPosition.Line,
                EndColumn = endPosition.Column,
            };
        }

        private (int Line, int Column) FindLineAndColumn(string content, string[] lines, int targetIndex)
        {
            int currentIndex = 0;

            for (int lineNum = 0; lineNum < lines.Length; lineNum++)
            {
                var lineEndIndex = currentIndex + lines[lineNum].Length;

                if (targetIndex <= lineEndIndex)
                {
                    return (lineNum + 1, targetIndex - currentIndex + 1); // Convert to 1-indexed
                }

                var separatorLength = GetNewlineLengthAtPosition(content, lineEndIndex);
                currentIndex = lineEndIndex + separatorLength;
            }

            return (1, 1); // Fallback to start if not found
        }

        private static (int Line, int Column) CalculateEndPosition(string body, (int Line, int Column) startPosition)
        {
            var bodyLines = body.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var endLine = startPosition.Line + bodyLines.Length - 1;
            var endColumn = bodyLines.Length == 1
                ? startPosition.Column + body.Length - 1
                : bodyLines[bodyLines.Length - 1].Length;

            return (endLine, endColumn);
        }

        /// <summary>
        /// Gets the length of the newline sequence at the specified position in the content.
        /// Returns 2 for CRLF, 1 for CR or LF, 0 if at EOF or no newline.
        /// </summary>
        private static int GetNewlineLengthAtPosition(string content, int position)
        {
            if (position >= content.Length)
            {
                return 0;
            }

            if (IsCrlfAt(content, position))
            {
                return 2;
            }

            var currentChar = content[position];
            return currentChar == '\r' || currentChar == '\n' ? 1 : 0;
        }

        private static bool IsCrlfAt(string content, int position)
        {
            return position + 1 < content.Length
                && content[position] == '\r'
                && content[position + 1] == '\n';
        }
    }
}
