// Copyright (c) CodeScene. All rights reserved.

using System.Text;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Tests
{
    [TestClass]
    public class ThemeStatusColorsTests
    {
        [TestMethod]
        [DataRow((byte)0x1F, (byte)0x1F, (byte)0x1F, false, DisplayName = "1F1F1F is dark")]
        [DataRow((byte)0x28, (byte)0x28, (byte)0x28, false, DisplayName = "282828 is dark")]
        [DataRow((byte)0xF7, (byte)0xF9, (byte)0xFE, true, DisplayName = "F7F9FE is light")]
        [DataRow((byte)0xF9, (byte)0xF9, (byte)0xF9, true, DisplayName = "F9F9F9 is light")]
        [DataRow((byte)0x00, (byte)0x00, (byte)0x00, false, DisplayName = "000000 is dark")]
        [DataRow((byte)0xFF, (byte)0xFF, (byte)0xFF, true, DisplayName = "FFFFFF is light")]
        public void IsLightBackground_ForKnownVsBackgrounds_ClassifiesLightAndDark(byte r, byte g, byte b, bool expectedIsLight)
        {
            var result = ThemeStatusColors.IsLightBackground(r, g, b);

            Assert.AreEqual(expectedIsLight, result);
        }

        [TestMethod]
        [DataRow((byte)0x80, (byte)0x80, (byte)0x80, true, DisplayName = "808080 is light")]
        [DataRow((byte)0x7F, (byte)0x7F, (byte)0x7F, false, DisplayName = "7F7F7F is dark")]
        public void IsLightBackground_AtTheThreshold_IsLight(byte r, byte g, byte b, bool expectedIsLight)
        {
            var result = ThemeStatusColors.IsLightBackground(r, g, b);

            Assert.AreEqual(expectedIsLight, result);
        }

        [TestMethod]
        public void IssueHex_OnLightBackground_ReturnsA15C00()
        {
            var result = ThemeStatusColors.IssueHex(0xFF, 0xFF, 0xFF);

            Assert.AreEqual("A15C00", result);
        }

        [TestMethod]
        public void IssueHex_OnDarkBackground_ReturnsEE9D28()
        {
            var result = ThemeStatusColors.IssueHex(0x00, 0x00, 0x00);

            Assert.AreEqual("EE9D28", result);
        }

        [TestMethod]
        public void AppendStatusVariables_EmitsTheThreeVariablesInOrder()
        {
            var sb = new StringBuilder();
            ThemeStatusColors.AppendStatusVariables(sb, "73C991", "C6A837", "EE9D28");

            var result = sb.ToString();
            var lines = result.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            Assert.AreEqual(3, lines.Length);
            Assert.AreEqual("  --cs-theme-success: #73C991;", lines[0]);
            Assert.AreEqual("  --cs-theme-warning: #C6A837;", lines[1]);
            Assert.AreEqual("  --cs-theme-issue: #EE9D28;", lines[2]);
        }
    }
}
