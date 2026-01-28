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
                    Startline = startLine,
                    EndLine = endLine,
                    StartColumn = startColumn,
                    EndColumn = endColumn
                }
            };
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
            Assert.AreEqual(3, result.UpdatedRange.Startline);
            Assert.AreEqual(1, fn.Range.Startline); // Original range should not be mutated
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
            Assert.AreEqual(6, result.UpdatedRange.Startline); // 1-indexed, line 6
            Assert.AreEqual(1, fn.Range.Startline); // Original range should not be mutated
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
    }
}
