// Copyright (c) CodeScene. All rights reserved.

using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;

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
        try
        {
            var dte = GetDteService();
            if (dte == null || !ThreadHelper.CheckAccess())
            {
                return string.Empty;
            }

            var editorFontFamily = GetEditorFontFamily(dte);
            var colors = GetThemeColors();

            var sb = new StringBuilder();

            AppendRootStart(sb);
            AppendBasicCssVariables(sb, editorFontFamily, colors);
            AppendOpacityVariants(sb, colors);
            AppendRootEnd(sb);

            return sb.ToString();
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static DTE GetDteService()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        return (DTE)Package.GetGlobalService(typeof(DTE));
    }

    private static string GetEditorFontFamily(DTE dte)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        var editorColorProps = dte.Properties["FontsAndColors", "TextEditor"];
        return editorColorProps.Item("FontFamily").Value.ToString();
    }

    private static (string TextFgHex, string EditorBgHex, string TextLinkFgHex, string CodeBlockBgHex, string ButtonFgHex, string ButtonBgHex, string SecondaryButtonFgHex, string SecondaryButtonBgHex) GetThemeColors()
    {
        var textForeground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
        var linkForeground = VSColorTheme.GetThemedColor(EnvironmentColors.HelpHowDoIPaneLinkColorKey);

        var editorBackground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
        var codeBlockBackground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBorderColorKey);

        bool isDarkTheme = editorBackground.Name == DarkThemeColorName || editorBackground.Name == DarkThemeColorNameVS2026;
        bool isLightTheme = editorBackground.Name == BlueThemeColorName || editorBackground.Name == LightThemeColorNameVS2026;

        var buttonForeground = VSColorTheme.GetThemedColor(EnvironmentColors.BrandedUITextColorKey);
        var buttonBackground = VSColorTheme.GetThemedColor(EnvironmentColors.EnvironmentBackgroundColorKey);

        var secondaryButtonForeground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
        var secondaryButtonBackground = VSColorTheme.GetThemedColor(EnvironmentColors.SystemButtonFaceColorKey);

        var textFgHex = ToHex(textForeground);
        var editorBgHex = ToHex(editorBackground);
        var textLinkFgHex = ToHex(linkForeground);
        var codeBlockBgHex = ToHex(codeBlockBackground);

        // For VS2022 Dark and Blue theme, and for VS2026 Dark and Light, we use a custom blue color. Other themes, use that themes primary color.
        var buttonFgHex = isLightTheme || isDarkTheme ? BlueBtnTextColorName : ToHex(buttonForeground);
        var buttonBgHex = isLightTheme || isDarkTheme ? BlueBtnBackgroundColorName : ToHex(buttonBackground);

        var secondaryButtonFgHex = ToHex(secondaryButtonForeground);
        var secondaryButtonBgHex = ToHex(secondaryButtonBackground);

        return (textFgHex, editorBgHex, textLinkFgHex, codeBlockBgHex, buttonFgHex, buttonBgHex, secondaryButtonFgHex, secondaryButtonBgHex);
    }

    private static void AppendRootStart(StringBuilder sb)
    {
        sb.AppendLine(":root {");
    }

    private static void AppendBasicCssVariables(StringBuilder sb, string editorFontFamily, (string TextFgHex, string EditorBgHex, string TextLinkFgHex, string CodeBlockBgHex, string ButtonFgHex, string ButtonBgHex, string SecondaryButtonFgHex, string SecondaryButtonBgHex) colors)
    {
        sb.AppendLine($"  --cs-theme-editor-background: #{colors.EditorBgHex};");
        sb.AppendLine($"  --cs-theme-textLink-foreground: #{colors.TextLinkFgHex};");
        sb.AppendLine($"  --cs-theme-panel-background: #{colors.TextFgHex};");
        sb.AppendLine($"  --cs-theme-textCodeBlock-background: #{colors.CodeBlockBgHex};");

        sb.AppendLine($"  --cs-theme-font-size: {FontSize}px;");
        sb.AppendLine($"  --cs-theme-editor-font-family: '{editorFontFamily}', monospace;");
        sb.AppendLine($"  --cs-theme-editor-font-size: {CodeBlockFontSize}px;");
    }

    private static void AppendOpacityVariants(StringBuilder sb, (string TextFgHex, string EditorBgHex, string TextLinkFgHex, string CodeBlockBgHex, string ButtonFgHex, string ButtonBgHex, string SecondaryButtonFgHex, string SecondaryButtonBgHex) colors)
    {
        // To be able to handle more variations of the colors (hover, active, selected, etc.) we add a few extra variants for certain variables:
        LoopOpacity(sb, "--cs-theme-foreground", colors.TextFgHex);
        LoopOpacity(sb, "--cs-theme-button-foreground", colors.ButtonFgHex);
        LoopOpacity(sb, "--cs-theme-button-background", colors.ButtonBgHex);
        LoopOpacity(sb, "--cs-theme-button-secondaryForeground", colors.SecondaryButtonFgHex);
        LoopOpacity(sb, "--cs-theme-button-secondaryBackground", colors.SecondaryButtonBgHex);
    }

    private static void AppendRootEnd(StringBuilder sb)
    {
        sb.AppendLine("}");
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
