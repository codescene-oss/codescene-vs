// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class TextUtilsTests
    {
        [TestMethod]
        public void ToSnakeCase_SpaceSeparatedWords_ReturnsSnakeCase()
            => Assert.AreEqual("hello_world", TextUtils.ToSnakeCase("Hello World"));

        [TestMethod]
        public void ToSnakeCase_HyphenSeparatedWords_ReturnsSnakeCase()
            => Assert.AreEqual("hello_world", TextUtils.ToSnakeCase("hello-world"));

        [TestMethod]
        public void ToSnakeCase_MixedCase_ReturnsLowerCase()
            => Assert.AreEqual("hello_world", TextUtils.ToSnakeCase("HELLO WORLD"));

        [TestMethod]
        public void ToSnakeCase_SingleWord_ReturnsLowerCase()
            => Assert.AreEqual("hello", TextUtils.ToSnakeCase("Hello"));

        [TestMethod]
        public void ToSnakeCase_MultipleSpaces_TreatedAsSingleSeparator()
            => Assert.AreEqual("hello_world", TextUtils.ToSnakeCase("Hello   World"));

        [TestMethod]
        public void ToSnakeCase_SpecialCharacters_AreRemoved()
            => Assert.AreEqual("hello_world", TextUtils.ToSnakeCase("Hello! World?"));

        [TestMethod]
        public void ToSnakeCase_Parentheses_AreRemoved_TextPreserved()
            => Assert.AreEqual("complex_method_high", TextUtils.ToSnakeCase("Complex Method (High)"));

        [TestMethod]
        public void ToSnakeCase_MixedSeparators_HandledCorrectly()
            => Assert.AreEqual("hello_beautiful_world", TextUtils.ToSnakeCase("Hello-Beautiful World"));

        [TestMethod]
        public void ToSnakeCase_LeadingAndTrailingSpaces_AreTrimmed()
            => Assert.AreEqual("hello_world", TextUtils.ToSnakeCase("  Hello World  "));

        [TestMethod]
        public void ToSnakeCase_EmptyString_ReturnsEmptyString()
            => Assert.AreEqual(string.Empty, TextUtils.ToSnakeCase(string.Empty));

        [TestMethod]
        public void ToSnakeCase_OnlySpecialCharacters_ReturnsEmptyString()
            => Assert.AreEqual(string.Empty, TextUtils.ToSnakeCase("!@#$%"));

        [TestMethod]
        public void ToSnakeCase_UnderscoresPreserved_WhenPartOfWord()
            => Assert.AreEqual("hello_world", TextUtils.ToSnakeCase("hello_world"));

        [TestMethod]
        public void ToSnakeCase_NumbersPreserved()
            => Assert.AreEqual("test123_value", TextUtils.ToSnakeCase("Test123 Value"));

        [TestMethod]
        public void NormalizeLineEndings_MixedLineEndings_ReturnsNormalized()
        {
            var input = "Line1\rLine2\nLine3\r\nLine4";
            var expected = $"Line1{Environment.NewLine}Line2{Environment.NewLine}Line3{Environment.NewLine}Line4";
            Assert.AreEqual(expected, TextUtils.NormalizeLineEndings(input));
        }

        [TestMethod]
        public void TrimForLogging_NullInput_ReturnsNull()
            => Assert.IsNull(TextUtils.TrimForLogging(null));

        [TestMethod]
        public void TrimForLogging_EmptyString_ReturnsEmptyString()
            => Assert.AreEqual(string.Empty, TextUtils.TrimForLogging(string.Empty));

        [TestMethod]
        public void TrimForLogging_StringShorterThanMax_ReturnsUnchanged()
            => Assert.AreEqual("short string", TextUtils.TrimForLogging("short string"));

        [TestMethod]
        public void TrimForLogging_StringExactlyAtMax_ReturnsUnchanged()
        {
            var input = new string('a', 120);
            Assert.AreEqual(input, TextUtils.TrimForLogging(input));
        }

        [TestMethod]
        public void TrimForLogging_StringLongerThanMax_TruncatesWithEllipsis()
        {
            var input = new string('a', 150);
            var expected = new string('a', 120) + "...";
            Assert.AreEqual(expected, TextUtils.TrimForLogging(input));
        }

        [TestMethod]
        public void TrimForLogging_CustomMaxLength_UsesCustomValue()
        {
            var input = "This is a test string";
            var expected = "This i...";
            Assert.AreEqual(expected, TextUtils.TrimForLogging(input, 6));
        }

        [TestMethod]
        public void ExtractLoggableJsonEntries_NullInput_ReturnsEmpty()
            => Assert.AreEqual(string.Empty, TextUtils.ExtractLoggableJsonEntries(null));

        [TestMethod]
        public void ExtractLoggableJsonEntries_EmptyString_ReturnsEmpty()
            => Assert.AreEqual(string.Empty, TextUtils.ExtractLoggableJsonEntries(string.Empty));

        [TestMethod]
        public void ExtractLoggableJsonEntries_InvalidJson_ReturnsPlainText()
            => Assert.AreEqual("not valid json", TextUtils.ExtractLoggableJsonEntries("not valid json"));

        [TestMethod]
        public void ExtractLoggableJsonEntries_LongInvalidJson_ReturnsTrimmedPlainText()
        {
            var longText = new string('x', 150);
            var result = TextUtils.ExtractLoggableJsonEntries(longText);

            Assert.AreEqual(123, result.Length);
            Assert.EndsWith("...", result);
        }

        [TestMethod]
        public void ExtractLoggableJsonEntries_ExcludesFileContent()
        {
            var json = "{\"path\":\"test.cs\",\"file-content\":\"huge content here\",\"cache-path\":\"/tmp\"}";
            var result = TextUtils.ExtractLoggableJsonEntries(json);

            Assert.DoesNotContain("file-content", result);
            Assert.DoesNotContain("huge content here", result);
        }

        [TestMethod]
        public void ExtractLoggableJsonEntries_IncludesOtherKeys()
        {
            var json = "{\"path\":\"test.cs\",\"cache-path\":\"/tmp\"}";
            var result = TextUtils.ExtractLoggableJsonEntries(json);

            Assert.Contains("'path'", result);
            Assert.Contains("\"test.cs\"", result);
            Assert.Contains("'cache-path'", result);
            Assert.Contains("\"/tmp\"", result);
        }

        [TestMethod]
        public void ExtractLoggableJsonEntries_TrimsLongValues()
        {
            var longValue = new string('a', 150);
            var json = $"{{\"key\":\"{longValue}\"}}";
            var result = TextUtils.ExtractLoggableJsonEntries(json);

            Assert.Contains("...", result);
            Assert.DoesNotContain(longValue, result);
        }

        [TestMethod]
        public void ExtractLoggableJsonEntries_HandlesNestedObjects()
        {
            var json = "{\"nested\":{\"inner\":\"value\"}}";
            var result = TextUtils.ExtractLoggableJsonEntries(json);

            Assert.Contains("'nested'", result);
        }

        [TestMethod]
        public void BuildCommandForLogging_NullContent_ReturnsTrimmededArgs()
        {
            var args = "review --format json";
            var result = TextUtils.BuildCommandForLogging(args, null);

            Assert.AreEqual(args, result);
        }

        [TestMethod]
        public void BuildCommandForLogging_EmptyContent_ReturnsTrimmedArgs()
        {
            var args = "review --format json";
            var result = TextUtils.BuildCommandForLogging(args, string.Empty);

            Assert.AreEqual(args, result);
        }

        [TestMethod]
        public void BuildCommandForLogging_InvalidJsonContent_IncludesPlainText()
        {
            var args = "review --format json";
            var result = TextUtils.BuildCommandForLogging(args, "not valid json");

            Assert.AreEqual("review --format json not valid json", result);
        }

        [TestMethod]
        public void BuildCommandForLogging_ValidContent_CombinesArgsAndEntries()
        {
            var args = "review";
            var json = "{\"path\":\"test.cs\",\"cache-path\":\"/tmp\"}";
            var result = TextUtils.BuildCommandForLogging(args, json);

            Assert.StartsWith("review ", result);
            Assert.Contains("'path'", result);
            Assert.Contains("\"test.cs\"", result);
            Assert.Contains("'cache-path'", result);
            Assert.Contains("\"/tmp\"", result);
        }

        [TestMethod]
        public void BuildCommandForLogging_ExcludesFileContent()
        {
            var args = "review";
            var json = "{\"path\":\"test.cs\",\"file-content\":\"huge content\"}";
            var result = TextUtils.BuildCommandForLogging(args, json);

            Assert.DoesNotContain("file-content", result);
            Assert.DoesNotContain("huge content", result);
        }
    }
}
