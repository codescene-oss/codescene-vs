// Copyright (c) CodeScene. All rights reserved.

using System;
using System.IO;
using System.Linq;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models.Agent;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Application.Agent
{
    internal static class AgentOutputMapper
    {
        public static RefactorResponseModel Map(
            AgentOutput output,
            string documentText,
            FnToRefactorModel fn,
            string path,
            ILogger logger)
        {
            var code = ApplyChanges(output.Changes, documentText, fn, path, logger);

            var isNoOp = code == fn.Body;
            var fixResult = isNoOp ? "unable_to_fix" : output.FixResult;
            var confidenceLevel = isNoOp ? "low" : output.Confidence;
            var summary = isNoOp ? "No changes proposed" : output.Summary;

            var confidence = MapConfidence(fixResult, confidenceLevel, summary, output.Reasoning);
            var reasons = MapReasons(fixResult, summary);
            var refactoringProperties = MapRefactoringProperties(fn.RefactoringTargets);

            return new RefactorResponseModel
            {
                Code = code,
                Confidence = confidence,
                Reasons = reasons,
                RefactoringProperties = refactoringProperties,
                Metadata = new MetadataModel { Cached = false },
                TraceId = output.TaskId,
                Declarations = null,
            };
        }

        private static string ApplyChanges(
            AgentChange[] changes,
            string documentText,
            FnToRefactorModel fn,
            string path,
            ILogger logger)
        {
            var documentFileName = Path.GetFileName(path);
            var relevantChanges = changes?.Where(c => Path.GetFileName(c.File) == documentFileName).ToArray() ?? new AgentChange[0];

            var result = fn.Body;
            var usesCrlf = result.Contains("\r\n");
            result = result.Replace("\r\n", "\n");

            foreach (var change in relevantChanges)
            {
                if (change.ChangeType == "whole_file" && !string.IsNullOrEmpty(change.WholeFileContent))
                {
                    result = ApplyWholeFileChange(change, documentText, fn, logger) ?? result;
                }
                else
                {
                    result = ApplyPartialChange(change, result, logger);
                }
            }

            if (usesCrlf)
            {
                result = result.Replace("\n", "\r\n");
            }

            return result;
        }

        private static string ApplyPartialChange(AgentChange change, string body, ILogger logger)
        {
            if (change.Replacements == null)
            {
                return body;
            }

            var result = body;
            foreach (var replacement in change.Replacements)
            {
                var search = ToLf(replacement.Search);
                var replace = ToLf(replacement.Replace);

                if (!TryReplaceFirst(ref result, search, replace)
                    && !TryReplaceFirst(ref result, search.TrimEnd('\n'), replace.TrimEnd('\n')))
                {
                    logger.Warn("Agent replacement pattern not found in function body, skipping replacement");
                }
            }

            return result;
        }

        private static bool TryReplaceFirst(ref string text, string search, string replace)
        {
            var index = string.IsNullOrEmpty(search) ? -1 : text.IndexOf(search, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            text = text.Substring(0, index) + replace + text.Substring(index + search.Length);
            return true;
        }

        private static string ToLf(string text) => (text ?? string.Empty).Replace("\r\n", "\n");

        private static string ApplyWholeFileChange(
            AgentChange change,
            string documentText,
            FnToRefactorModel fn,
            ILogger logger)
        {
            var documentNormalized = documentText.Replace("\r\n", "\n");
            var wholeFileContentNormalized = change.WholeFileContent.Replace("\r\n", "\n");

            var startPos = GetPositionFromRange(documentNormalized, fn.Range.StartLine, fn.Range.StartColumn);
            var endPos = GetPositionFromRange(documentNormalized, fn.Range.EndLine, fn.Range.EndColumn);

            if (!IsValidRange(startPos, endPos, documentNormalized.Length))
            {
                logger.Warn("Agent whole-file change ignored: invalid function range");
                return null;
            }

            var prefix = documentNormalized.Substring(0, startPos);
            var suffix = documentNormalized.Substring(endPos);

            if (!FitsAroundFunction(wholeFileContentNormalized, prefix, suffix))
            {
                logger.Warn("Agent whole-file change ignored: content outside function range was modified");
                return null;
            }

            var newBody = wholeFileContentNormalized.Substring(prefix.Length, wholeFileContentNormalized.Length - prefix.Length - suffix.Length);
            return newBody;
        }

        private static bool IsValidRange(int startPos, int endPos, int length)
        {
            return startPos >= 0 && startPos <= endPos && endPos <= length;
        }

        private static bool FitsAroundFunction(string content, string prefix, string suffix)
        {
            if (content.Length < prefix.Length + suffix.Length)
            {
                return false;
            }

            return content.StartsWith(prefix, StringComparison.Ordinal) && content.EndsWith(suffix, StringComparison.Ordinal);
        }

        private static int GetPositionFromRange(string text, int line, int column)
        {
            var lines = text.Split('\n');
            if (line < 1 || line > lines.Length)
            {
                return -1;
            }

            var position = 0;
            for (int i = 0; i < line - 1; i++)
            {
                position += lines[i].Length + 1;
            }

            position += column - 1;
            return position;
        }

        private static int MapLevel(string confidenceLevel)
        {
            switch (confidenceLevel?.ToLower())
            {
                case "high":
                    return 3;
                case "low":
                    return 1;
                default:
                    return 2;
            }
        }

        private static ConfidenceModel MapConfidence(string fixResult, string confidenceLevel, string summary, string reasoning)
        {
            var level = MapLevel(confidenceLevel);

            string title;
            string recommendedActionDescription;
            string reviewHeader;

            switch (fixResult)
            {
                case "fix_proposed":
                    title = summary;
                    recommendedActionDescription = "Review the suggested changes and apply if appropriate.";
                    reviewHeader = null;
                    break;
                case "unable_to_fix":
                    title = "Unable to refactor";
                    recommendedActionDescription = "The agent could not find a suitable refactoring for this code.";
                    reviewHeader = "Review required";
                    break;
                case "needs_human_review":
                    title = "Needs review";
                    recommendedActionDescription = "The suggested changes require careful human review before applying.";
                    reviewHeader = "Review required";
                    break;
                default:
                    title = summary;
                    recommendedActionDescription = "Review the suggested changes and apply if appropriate.";
                    reviewHeader = "Review required";
                    break;
            }

            return new ConfidenceModel
            {
                Level = level,
                Title = title,
                ReviewHeader = reviewHeader,
                RecommendedAction = new RecommendedActionModel
                {
                    Description = recommendedActionDescription,
                    Details = reasoning,
                },
            };
        }

        private static ReasonModel[] MapReasons(string fixResult, string summary)
        {
            if (fixResult == "fix_proposed")
            {
                return new ReasonModel[0];
            }

            return new[] { new ReasonModel { Summary = summary } };
        }

        private static RefactoringPropertiesModel MapRefactoringProperties(RefactoringTargetModel[] targets)
        {
            var removedCodeSmells = targets?.Select(t => t.Category).Distinct().ToArray() ?? new string[0];

            return new RefactoringPropertiesModel
            {
                AddedCodeSmells = new string[0],
                RemovedCodeSmells = removedCodeSmells,
            };
        }
    }
}
