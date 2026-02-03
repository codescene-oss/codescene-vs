// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Ace;
using Codescene.VSExtension.Core.Models.Cli;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class AceStaleCheckerTests
    {
        private AceStaleChecker _checker;

        [TestInitialize]
        public void Setup()
        {
            _checker = new AceStaleChecker();
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_BodyMatchesAtRange_ReturnsNotStale()
        {
            var body = "function test() { return 1; }";
            var content = $"line1\n{body}\nline3";
            var fn = CreateFnToRefactor(body: body, startLine: 2, endLine: 2, startColumn: 1, endColumn: body.Length);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_BodyNotFound_ReturnsStale()
        {
            var originalBody = "function test() { return 1; }";
            var changedBody = "function changed() { return 2; }";
            var content = $"line1\n{changedBody}\nline3";
            var fn = CreateFnToRefactor(body: originalBody, startLine: 2, endLine: 2);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsTrue(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_BodyMovedElsewhere_ReturnsNotStaleWithRangeUpdate()
        {
            var body = "function test() { return 1; }";
            var content = $"newline\nnewline2\n{body}";

            // Original position was line 1
            var fn = CreateFnToRefactor(body: body, startLine: 1, endLine: 1);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsTrue(result.RangeUpdated);

            // Updated range should point to line 3 (1-indexed), original fn.Range should be unchanged
            Assert.IsNotNull(result.UpdatedRange);
            Assert.AreEqual(3, result.UpdatedRange.StartLine);
            Assert.AreEqual(1, fn.Range.StartLine); // Original range should not be mutated
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_NullBody_ReturnsNotStale()
        {
            var fn = CreateFnToRefactor(body: null, startLine: 1, endLine: 1);

            var result = _checker.IsFunctionUnchangedInDocument("some content", fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_NullFnToRefactor_ReturnsNotStale()
        {
            var result = _checker.IsFunctionUnchangedInDocument("some content", null);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_EmptyContent_ReturnsNotStale()
        {
            var fn = CreateFnToRefactor(body: "function test() {}", startLine: 1, endLine: 1);

            var result = _checker.IsFunctionUnchangedInDocument(string.Empty, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_NullContent_ReturnsNotStale()
        {
            var fn = CreateFnToRefactor(body: "function test() {}", startLine: 1, endLine: 1);

            var result = _checker.IsFunctionUnchangedInDocument(null, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_MultiLineFunction_BodyMatchesAtRange_ReturnsNotStale()
        {
            var body = "function test() {\n    return 1;\n}";
            var content = $"// header\n{body}\n// footer";
            var fn = CreateFnToRefactor(body: body, startLine: 2, endLine: 4, startColumn: 1, endColumn: 1);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_MultiLineFunction_BodyModified_ReturnsStale()
        {
            var originalBody = "function test() {\n    return 1;\n}";
            var modifiedBody = "function test() {\n    return 2;\n}";
            var content = $"// header\n{modifiedBody}\n// footer";
            var fn = CreateFnToRefactor(body: originalBody, startLine: 2, endLine: 4, startColumn: 1, endColumn: 1);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsTrue(result.IsStale);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_FunctionMovedDown_ReturnsUpdatedRangeWithoutMutatingOriginal()
        {
            var body = "function test() { return 1; }";

            // Add 5 new lines before the function
            var content = $"line1\nline2\nline3\nline4\nline5\n{body}";
            var fn = CreateFnToRefactor(body: body, startLine: 1, endLine: 1);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsTrue(result.RangeUpdated);
            Assert.IsNotNull(result.UpdatedRange);
            Assert.AreEqual(6, result.UpdatedRange.StartLine); // 1-indexed, line 6
            Assert.AreEqual(1, fn.Range.StartLine); // Original range should not be mutated
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_SingleCharacterChange_ReturnsStale()
        {
            var originalBody = "function test() { return 1; }";
            var modifiedBody = "function test() { return 2; }"; // Changed 1 to 2
            var content = $"line1\n{modifiedBody}\nline3";
            var fn = CreateFnToRefactor(body: originalBody, startLine: 2, endLine: 2);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsTrue(result.IsStale);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_WhitespaceOnlyChange_ReturnsStale()
        {
            var originalBody = "function test() { return 1; }";
            var modifiedBody = "function test()  { return 1; }"; // Extra space
            var content = $"line1\n{modifiedBody}\nline3";
            var fn = CreateFnToRefactor(body: originalBody, startLine: 2, endLine: 2);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsTrue(result.IsStale);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_NullRange_ReturnsStale()
        {
            // When range is null, GetContentAtRange returns empty string,
            // which won't match the body, and body won't be found, so it's stale
            var fn = new FnToRefactorModel
            {
                Name = "TestFunction",
                Body = "function test() {}",
                Range = null,
            };

            var result = _checker.IsFunctionUnchangedInDocument("some other content", fn);

            Assert.IsTrue(result.IsStale);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_InvalidRange_NegativeStartLine_ReturnsStale()
        {
            // Range with startLine = 0 (which becomes -1 when 0-indexed) is invalid
            var fn = CreateFnToRefactor(body: "function test() {}", startLine: 0, endLine: 1);

            var result = _checker.IsFunctionUnchangedInDocument("some other content", fn);

            Assert.IsTrue(result.IsStale);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_InvalidRange_StartLineGreaterThanLineCount_ReturnsStale()
        {
            var content = "line1\nline2";
            var fn = CreateFnToRefactor(body: "function test() {}", startLine: 100, endLine: 100);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsTrue(result.IsStale);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_InvalidRange_StartLineGreaterThanEndLine_ReturnsStale()
        {
            var content = "line1\nline2\nline3";
            var fn = CreateFnToRefactor(body: "function test() {}", startLine: 3, endLine: 1);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsTrue(result.IsStale);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_CrlfNewlines_BodyMatchesAtRange_ReturnsNotStale()
        {
            var body = "function test() { return 1; }";
            var content = $"line1\r\n{body}\r\nline3";
            var fn = CreateFnToRefactor(body: body, startLine: 2, endLine: 2, startColumn: 1, endColumn: body.Length);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_CrOnlyNewlines_BodyMatchesAtRange_ReturnsNotStale()
        {
            var body = "function test() { return 1; }";
            var content = $"line1\r{body}\rline3";
            var fn = CreateFnToRefactor(body: body, startLine: 2, endLine: 2, startColumn: 1, endColumn: body.Length);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_NoNewlines_SingleLineContent_ReturnsNotStale()
        {
            var body = "function test() { return 1; }";
            var content = body; // No newlines at all
            var fn = CreateFnToRefactor(body: body, startLine: 1, endLine: 1, startColumn: 1, endColumn: body.Length);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_StartColumnExceedsLineLength_ReturnsStale()
        {
            var body = "x";
            var content = "short\nline2";

            // StartColumn 100 is way beyond "short" (length 5)
            var fn = CreateFnToRefactor(body: body, startLine: 1, endLine: 1, startColumn: 100, endColumn: 101);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsTrue(result.IsStale);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_MultiLine_CrlfNewlines_BodyMatchesAtRange_ReturnsNotStale()
        {
            var body = "function test() {\r\n    return 1;\r\n}";
            var content = $"// header\r\n{body}\r\n// footer";
            var fn = CreateFnToRefactor(body: body, startLine: 2, endLine: 4, startColumn: 1, endColumn: 1);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_MultiLine_CrOnlyNewlines_BodyMatchesAtRange_ReturnsNotStale()
        {
            var body = "function test() {\r    return 1;\r}";
            var content = $"// header\r{body}\r// footer";
            var fn = CreateFnToRefactor(body: body, startLine: 2, endLine: 4, startColumn: 1, endColumn: 1);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_MultiLineFunction_BodyMovedWithCrlf_ReturnsUpdatedRange()
        {
            var body = "function test() {\r\n    return 1;\r\n}";
            var content = $"line1\r\nline2\r\n{body}";
            var fn = CreateFnToRefactor(body: body, startLine: 1, endLine: 3, startColumn: 1, endColumn: 1);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsTrue(result.RangeUpdated);
            Assert.IsNotNull(result.UpdatedRange);
            Assert.AreEqual(3, result.UpdatedRange.StartLine);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_EndLineAtLastLine_ReturnsNotStale()
        {
            // Test AppendLastLine when endLine equals last line index
            var body = "function test() {\n    return 1;\n}";
            var content = body; // Body is the entire content
            var fn = CreateFnToRefactor(body: body, startLine: 1, endLine: 3, startColumn: 1, endColumn: 1);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_EndLineBeyondContent_ClampsToLastLine_ReturnsNotStale()
        {
            var body = "function test() {\n    return 1;\n}";
            var content = body;

            // EndLine 100 is beyond content, should be clamped
            var fn = CreateFnToRefactor(body: body, startLine: 1, endLine: 100, startColumn: 1, endColumn: 1);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_SingleLineExtraction_WithPartialColumns_ReturnsNotStale()
        {
            var fullLine = "prefix function test() { return 1; } suffix";
            var body = "function test() { return 1; }";
            var content = $"line1\n{fullLine}\nline3";

            // Extract only the middle part using columns
            // StartColumn is 1-indexed, EndColumn is used as exclusive boundary for substring
            var startIdx = fullLine.IndexOf("function"); // 0-indexed: 7
            var startCol = startIdx + 1; // 1-indexed: 8
            var endCol = startIdx + body.Length; // Exclusive end for substring: 36
            var fn = CreateFnToRefactor(body: body, startLine: 2, endLine: 2, startColumn: startCol, endColumn: endCol);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        [TestMethod]
        public void IsFunctionUnchangedInDocument_MultiLineExtraction_OnlyFirstAndLastLine_ReturnsNotStale()
        {
            // Test case where StartLine == EndLine - 1 (no middle lines)
            var body = "function test() {\n}";
            var content = $"// header\n{body}\n// footer";
            var fn = CreateFnToRefactor(body: body, startLine: 2, endLine: 3, startColumn: 1, endColumn: 1);

            var result = _checker.IsFunctionUnchangedInDocument(content, fn);

            Assert.IsFalse(result.IsStale);
            Assert.IsFalse(result.RangeUpdated);
        }

        private static FnToRefactorModel CreateFnToRefactor(
            string body,
            int startLine,
            int endLine,
            int startColumn = 1,
            int endColumn = 100)
        {
            return new FnToRefactorModel
            {
                Name = "TestFunction",
                Body = body,
                Range = new CliRangeModel
                {
                    StartLine = startLine,
                    EndLine = endLine,
                    StartColumn = startColumn,
                    EndColumn = endColumn,
                },
            };
        }
    }
}
