// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Util;

public static class StyleHelper
{
    private static readonly string DarkThemeColorName = "ff1f1f1f";
    private static readonly string BlueThemeColorName = "fff7f9fe";
    private static readonly string DarkThemeColorNameVS2026 = "ff282828";
    private static readonly string LightThemeColorNameVS2026 = "fff9f9f9";

    private static readonly string BlueBtnTextColorName = "fafafa";
    private static readonly string BlueBtnBackgroundColorName = "0c517b";

    private static readonly int FontSize = 13;
    private static readonly int CodeBlockFontSize = 13;

    private static readonly Dictionary<int, string> OpacityVariants = new Dictionary<int, string>
    {
        [1] = "03",
        [3] = "08",
        [7] = "12",
        [10] = "1A",
        [20] = "33",
        [30] = "4D",
        [40] = "66",
        [50] = "80",
        [60] = "99",
        [70] = "B3",
        [75] = "BF",
        [80] = "CC",
        [85] = "D9",
        [90] = "E6",
    };

    /// <summary>
    /// Generates a CSS string defining theme variables based on the current Visual Studio color theme.
    /// These variables are injected into the WebView to match the IDE's appearance.
    /// The method reads Visual Studio color settings (like foreground, background, link colors, etc.)
    /// and constructs a CSS `:root` block containing corresponding CSS custom properties.
    ///
    /// The generated CSS includes:
    /// <list type="bullet">
    ///   <item><description><c>--cs-theme-editor-background</c>: Background color for the editor area.</description></item>
    ///   <item><description><c>--cs-theme-textLink-foreground</c>: Text color for hyperlinks.</description></item>
    ///   <item><description><c>--cs-theme-foreground</c>: General foreground color.</description></item>
    ///   <item><description><c>--cs-theme-panel-background</c>: Background color for panels (currently uses the foreground as a placeholder).</description></item>
    ///   <item><description><c>--cs-theme-textCodeBlock-background</c>: Background color for code blocks.</description></item>
    ///   <item><description><c>--cs-theme-editor-font-family</c>: Font family used in the editor.</description></item>
    ///   <item><description><c>--cs-theme-editor-font-size</c>: Font size used in the editor.</description></item>
    ///   <item><description><c>--cs-theme-button-foreground</c>: Primary button text color.</description></item>
    ///   <item><description><c>--cs-theme-button-background</c>: Primary button background color.</description></item>
    ///   <item><description><c>--cs-theme-button-secondaryForeground</c>: Secondary button text color.</description></item>
    ///   <item><description><c>--cs-theme-button-secondaryBackground</c>: Secondary button background color (may use fallback for dark themes).</description></item>
    /// </list>
    ///
    /// <note>
    /// IDE-wide font settings (e.g. <c>--cs-theme-font-family</c>, <c>--cs-theme-font-size</c>) are not currently included,
    /// as there is no reliable way to retrieve them via the Visual Studio API.
    /// </note>
    ///
    /// Additionally, it appends several opacity-based variants (e.g. <c>--cs-theme-foreground-10</c>) for hover/active styling.
    ///
    /// Returns an empty string if the theme data couldn't be accessed (e.g., DTE is unavailable or called from a non-UI thread).
    /// </summary>
    public static string GenerateCssVariablesFromTheme()
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        try
        {
            var dte = (DTE)Package.GetGlobalService(typeof(DTE));
            if (dte == null || !ThreadHelper.CheckAccess())
            {
                return string.Empty;
            }

            var editorColorProps = dte.Properties["FontsAndColors", "TextEditor"];
            var editorFontFamily = editorColorProps.Item("FontFamily").Value.ToString();

            var textForeground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
            var linkForeground = VSColorTheme.GetThemedColor(EnvironmentColors.HelpHowDoIPaneLinkColorKey);

            var editorBackground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
            var codeBlockBackground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBorderColorKey);

            bool isDarkTheme = editorBackground.Name == DarkThemeColorName || editorBackground.Name == DarkThemeColorNameVS2026;
            bool isLightTheme = editorBackground.Name == BlueThemeColorName || editorBackground.Name == LightThemeColorNameVS2026;

            var buttonForeground = VSColorTheme.GetThemedColor(EnvironmentColors.BrandedUITextColorKey);
            var buttonBackground = VSColorTheme.GetThemedColor(EnvironmentColors.EnvironmentBackgroundColorKey);

            var secondaryButtonForeground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
            var secondaryButtonBackground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowButtonDownActiveGlyphBrushKey);

            var textFgHex = ToHex(textForeground);
            var editorBgHex = ToHex(editorBackground);
            var textLinkFgHex = ToHex(linkForeground);
            var codeBlockBgHex = ToHex(codeBlockBackground);

            // For VS2022 Dark and Blue theme, and for VS2026 Dark and Light, we use a custom blue color. Other themes, use that themes primary color.
            var buttonFgHex = isLightTheme || isDarkTheme ? BlueBtnTextColorName : ToHex(buttonForeground);
            var buttonBgHex = isLightTheme || isDarkTheme ? BlueBtnBackgroundColorName : ToHex(buttonBackground);

            var secondaryButtonFgHex = ToHex(secondaryButtonForeground);
            var secondaryButtonBgHex = ToHex(secondaryButtonBackground);

            var sb = new StringBuilder();

            sb.AppendLine(":root {");
            sb.AppendLine($"  --cs-theme-editor-background: #{editorBgHex};");
            sb.AppendLine($"  --cs-theme-textLink-foreground: #{textLinkFgHex};");
            sb.AppendLine($"  --cs-theme-panel-background: #{textFgHex};");
            sb.AppendLine($"  --cs-theme-textCodeBlock-background: #{codeBlockBgHex};");

            sb.AppendLine($"  --cs-theme-font-size: {FontSize}px;");
            sb.AppendLine($"  --cs-theme-editor-font-family: '{editorFontFamily}', monospace;");
            sb.AppendLine($"  --cs-theme-editor-font-size: {CodeBlockFontSize}px;");

            // To be able to handle more variations of the colors (hover, active, selected, etc.) we add a few extra variants for certain variables:
            LoopOpacity(sb, "--cs-theme-foreground", textFgHex);
            LoopOpacity(sb, "--cs-theme-button-foreground", buttonFgHex);
            LoopOpacity(sb, "--cs-theme-button-background", buttonBgHex);
            LoopOpacity(sb, "--cs-theme-button-secondaryForeground", secondaryButtonFgHex);
            LoopOpacity(sb, "--cs-theme-button-secondaryBackground", secondaryButtonBgHex);

            sb.AppendLine("}");

            return sb.ToString();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static void LoopOpacity(StringBuilder sb, string baseName, string hex)
    {
        sb.AppendLine($"  {baseName}: #{hex};");
        foreach (var pair in OpacityVariants)
        {
            //sb.AppendLine($"  {baseName}-{pair.Key}: #{hex}{pair.Value};");
            sb.AppendLine($"  {baseName}-{pair.Key}: color-mix(in srgb, var({baseName}) {pair.Key}%, transparent);");
        }
    }

    private static string ToHex(Color c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";
}
