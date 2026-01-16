using Codescene.VSExtension.Core.Application.Services.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace Codescene.VSExtension.CoreTests
{
    [TestClass]
    public class PasteArgumentsTests
    {
        [TestMethod]
        public void AppendArgument_SimpleArgument_NoQuotes()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, "simple");
            Assert.AreEqual("simple", sb.ToString());
        }

        [TestMethod]
        public void AppendArgument_MultipleSimpleArguments_SpaceSeparated()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, "arg1");
            PasteArguments.AppendArgument(sb, "arg2");
            PasteArguments.AppendArgument(sb, "arg3");
            Assert.AreEqual("arg1 arg2 arg3", sb.ToString());
        }

        [TestMethod]
        public void AppendArgument_ArgumentWithSpace_QuotesAdded()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, "hello world");
            Assert.AreEqual("\"hello world\"", sb.ToString());
        }

        [TestMethod]
        public void AppendArgument_ArgumentWithQuote_QuoteEscaped()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, "say \"hello\"");
            Assert.AreEqual("\"say \\\"hello\\\"\"", sb.ToString());
        }

        [TestMethod]
        public void AppendArgument_EmptyString_QuotesAdded()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, "");
            Assert.AreEqual("\"\"", sb.ToString());
        }

        [TestMethod]
        public void AppendArgument_ArgumentWithBackslash_Preserved()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, @"C:\path\to\file");
            // Backslash is normal character when not followed by quote
            Assert.AreEqual(@"C:\path\to\file", sb.ToString());
        }

        [TestMethod]
        public void AppendArgument_ArgumentWithBackslashAndSpace_QuotedWithBackslashDoubled()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, @"C:\path with space\file");
            Assert.AreEqual("\"C:\\path with space\\file\"", sb.ToString());
        }

        [TestMethod]
        public void AppendArgument_ArgumentEndingWithBackslash_BackslashDoubled()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, @"C:\path with space\file\");
            // When argument ends with backslash and needs quoting, backslash is doubled
            Assert.AreEqual("\"C:\\path with space\\file\\\\\"", sb.ToString());
        }

        [TestMethod]
        public void AppendArgument_ArgumentWithBackslashBeforeQuote_BackslashDoubled()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, "test\\\"value");
            // Backslash before quote needs special handling
            Assert.AreEqual("\"test\\\\\\\"value\"", sb.ToString());
        }

        [TestMethod]
        public void AppendArgument_ArgumentWithMultipleBackslashesBeforeQuote_AllDoubled()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, "test\\\\\"value");
            // Multiple backslashes before quote
            Assert.AreEqual("\"test\\\\\\\\\\\"value\"", sb.ToString());
        }

        [TestMethod]
        public void AppendArgument_JsonContent_ProperlyEscaped()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, "{\"key\":\"value\"}");
            Assert.AreEqual("\"{\\\"key\\\":\\\"value\\\"}\"", sb.ToString());
        }

        [TestMethod]
        public void AppendArgument_PathLikeArgument_NoQuotesNeeded()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, "review");
            PasteArguments.AppendArgument(sb, "--file-name");
            PasteArguments.AppendArgument(sb, "test.cs");
            Assert.AreEqual("review --file-name test.cs", sb.ToString());
        }

        [TestMethod]
        public void AppendArgument_TabCharacter_QuotesAdded()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, "hello\tworld");
            Assert.AreEqual("\"hello\tworld\"", sb.ToString());
        }

        [TestMethod]
        public void AppendArgument_NewlineCharacter_QuotesAdded()
        {
            var sb = new StringBuilder();
            PasteArguments.AppendArgument(sb, "hello\nworld");
            Assert.AreEqual("\"hello\nworld\"", sb.ToString());
        }
    }
}
