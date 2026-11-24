// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Codescene.VSExtension.Core.Enums;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Application;

public static class FunctionToRefactorResolver
{
    public static FnToRefactorModel ForCodeSmell(string path, string content, CodeSmellModel smell)
    {
        var target = new RefactoringTargetModel
        {
            Category = smell.Category,
            Line = smell.Range.StartLine,
        };

        if (smell.FunctionRange != null)
        {
            var range = ToWholeLinesRange(smell.FunctionRange, content);
            var body = ExtractBody(content, range);
            var extension = GetExtensionWithoutDot(path);

            return new FnToRefactorModel
            {
                Name = smell.FunctionName,
                Body = body,
                FileType = extension,
                FunctionType = extension,
                Range = range,
                RefactoringTargets = new[] { target },
            };
        }
        else
        {
            var fileName = Path.GetFileName(path);
            var range = ToWholeFileRange(content);
            var body = ExtractBody(content, range);
            var extension = GetExtensionWithoutDot(path);

            return new FnToRefactorModel
            {
                Name = fileName,
                Body = body,
                FileType = extension,
                FunctionType = extension,
                Range = range,
                RefactoringTargets = new[] { target },
            };
        }
    }

    public static FnToRefactorModel ForFinding(string path, string content, FunctionFindingModel finding)
    {
        if (finding.Function?.Range == null)
        {
            return null;
        }

        var targets = finding.ChangeDetails?
            .Where(cd => cd.ChangeType != ChangeType.Fixed && cd.Line.HasValue)
            .Select(cd => new RefactoringTargetModel
            {
                Category = cd.Category,
                Line = cd.Line.Value,
            })
            .ToArray();

        if (targets == null || targets.Length == 0)
        {
            return null;
        }

        var functionRange = new CodeRangeModel(
            finding.Function.Range.StartLine,
            finding.Function.Range.EndLine,
            finding.Function.Range.StartColumn,
            finding.Function.Range.EndColumn);

        var range = ToWholeLinesRange(functionRange, content);
        var body = ExtractBody(content, range);
        var extension = GetExtensionWithoutDot(path);

        return new FnToRefactorModel
        {
            Name = finding.Function.Name,
            Body = body,
            FileType = extension,
            FunctionType = extension,
            Range = range,
            RefactoringTargets = targets,
        };
    }

    public static FnToRefactorModel ForWholeFile(string path, string content, RefactoringTargetModel[] targets)
    {
        var fileName = Path.GetFileName(path);
        var range = ToWholeFileRange(content);
        var body = ExtractBody(content, range);
        var extension = GetExtensionWithoutDot(path);

        return new FnToRefactorModel
        {
            Name = fileName,
            Body = body,
            FileType = extension,
            FunctionType = extension,
            Range = range,
            RefactoringTargets = targets,
        };
    }

    private static CliRangeModel ToWholeLinesRange(CodeRangeModel range, string content)
    {
        var lines = SplitLines(content);
        var lineCount = lines.Length;

        var startLine = range.StartLine;
        var endLine = Math.Min(Math.Max(range.EndLine, startLine), lineCount);

        var endLineText = endLine > 0 && endLine <= lines.Length ? lines[endLine - 1] : string.Empty;
        var endColumn = endLineText.Length + 1;

        return new CliRangeModel
        {
            StartLine = startLine,
            StartColumn = 1,
            EndLine = endLine,
            EndColumn = endColumn,
        };
    }

    private static CliRangeModel ToWholeFileRange(string content)
    {
        var lines = SplitLines(content);
        var lineCount = lines.Length;

        if (lineCount == 0)
        {
            return new CliRangeModel
            {
                StartLine = 1,
                StartColumn = 1,
                EndLine = 1,
                EndColumn = 1,
            };
        }

        var lastLine = lines[lineCount - 1];
        var endColumn = lastLine.Length + 1;

        return new CliRangeModel
        {
            StartLine = 1,
            StartColumn = 1,
            EndLine = lineCount,
            EndColumn = endColumn,
        };
    }

    private static string ExtractBody(string content, CliRangeModel range)
    {
        var lines = SplitLines(content);

        if (range.StartLine < 1 || range.StartLine > lines.Length)
        {
            return string.Empty;
        }

        var startIndex = range.StartLine - 1;
        var endIndex = Math.Min(range.EndLine, lines.Length) - 1;

        var selectedLines = new List<string>();
        for (int i = startIndex; i <= endIndex; i++)
        {
            selectedLines.Add(lines[i]);
        }

        var lineEnding = DetectLineEnding(content);
        return string.Join(lineEnding, selectedLines);
    }

    private static string[] SplitLines(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return Array.Empty<string>();
        }

        return content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
    }

    private static string DetectLineEnding(string content)
    {
        if (content.Contains("\r\n"))
        {
            return "\r\n";
        }

        return "\n";
    }

    private static string GetExtensionWithoutDot(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            return string.Empty;
        }

        return extension.TrimStart('.');
    }
}
