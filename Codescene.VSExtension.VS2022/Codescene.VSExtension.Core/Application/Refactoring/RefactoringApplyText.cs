// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Models;

namespace Codescene.VSExtension.Core.Application.Refactoring
{
    public static class RefactoringApplyText
    {
        private static readonly IndentationService IndentationService = new IndentationService();

        public static string Prepare(string code, Func<IndentationInfo> detectIndentation)
        {
            if (string.IsNullOrEmpty(code))
            {
                return code;
            }

            var startsWithWhitespace = code.Length > 0 && char.IsWhiteSpace(code[0]);

            if (startsWithWhitespace)
            {
                return code;
            }

            var indentationInfo = detectIndentation();

            if (indentationInfo.Level > 0)
            {
                return IndentationService.AdjustIndentation(code, indentationInfo);
            }

            return code;
        }
    }
}
