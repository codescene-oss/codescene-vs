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
    }
}
