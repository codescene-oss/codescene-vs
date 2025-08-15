using EnvDTE;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace Codescene.VSExtension.VS2022.Util;

public static class StyleHelper
{
    private static string DarkThemeColorName = "ff1f1f1f";
    private static string DarkThemeFallbackSecondaryBg = "0c517b";
    private static string BlueThemeColorName = "fff7f9fe";
    private static string DarkAndLightThemeBtnTextColorName = "fafafa";

    private static Dictionary<int, string> opacityVariants = new Dictionary<int, string>
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
        [90] = "E6"
    };

    private static string ToHex(Color c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";

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
            var dte = (DTE)Package.GetGlobalService(typeof(DTE));
            if (dte == null || !ThreadHelper.CheckAccess()) return "";

            var editorColorProps = dte.get_Properties("FontsAndColors", "TextEditor");
            var editorFontFamily = editorColorProps.Item("FontFamily").Value.ToString();
            var editorFontSize = Convert.ToDouble(editorColorProps.Item("FontSize").Value);

            var textForeground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
            var linkForeground = VSColorTheme.GetThemedColor(EnvironmentColors.HelpHowDoIPaneLinkColorKey);

            var editorBackground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
            var codeBlockBackground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBorderColorKey);

            var buttonForeground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
            var buttonBackground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowButtonHoverActiveColorKey);

            //var buttonForeground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowTextColorKey);
            //var buttonBackground = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowButtonActiveGlyphColorKey);

            var textFg = ToHex(textForeground);
            var buttonFgHex = editorBackground.Name == BlueThemeColorName ? ToHex(buttonForeground) : DarkAndLightThemeBtnTextColorName;
            //var buttonBgHex = ToHex(buttonBackground);

            var buttonBgHex = editorBackground.Name == DarkThemeColorName
               ? DarkThemeFallbackSecondaryBg
               : ToHex(buttonBackground);

            var editorBgHex = ToHex(editorBackground);
            var textLinkFgHex = ToHex(linkForeground);
            var codeBlockBgHex = ToHex(codeBlockBackground);

            var secondaryButtonBgHex = editorBackground.Name == DarkThemeColorName
                ? DarkThemeFallbackSecondaryBg
                : buttonBgHex;

            var sb = new StringBuilder();

            sb.AppendLine(":root {");
            sb.AppendLine($"  --cs-theme-editor-background: #{editorBgHex};");
            sb.AppendLine($"  --cs-theme-textLink-foreground: #{textLinkFgHex};");
            sb.AppendLine($"  --cs-theme-foreground: #{textFg};");
            sb.AppendLine($"  --cs-theme-panel-background: #{textFg};");
            sb.AppendLine($"  --cs-theme-textCodeBlock-background: #{codeBlockBgHex};");

            sb.AppendLine($"  --cs-theme-editor-font-family: '{editorFontFamily}', monospace;");
            sb.AppendLine($"  --cs-theme-editor-font-size: {editorFontSize}px;");

            sb.AppendLine($"  --cs-theme-button-foreground: #{buttonFgHex};");
            sb.AppendLine($"  --cs-theme-button-background: #{buttonBgHex};");
            sb.AppendLine($"  --cs-theme-button-secondaryForeground: #{buttonFgHex};");
            sb.AppendLine($"  --cs-theme-button-secondaryBackground: #{secondaryButtonBgHex};");

            // To be able to handle more variations of the colors (hover, active, selected, etc) we add a few extra variants for certain variables:
            foreach (var pair in opacityVariants)
            {
                sb.AppendLine($"  --cs-theme-button-foreground-{pair.Key}: #{textFg}{pair.Value};");
                sb.AppendLine($"  --cs-theme-button-background-{pair.Key}: #{buttonBgHex}{pair.Value};");

                sb.AppendLine($"  --cs-theme-foreground-{pair.Key}: #{textFg}{pair.Value};");

                sb.AppendLine($"  --cs-theme-button-secondaryBackground-{pair.Key}: #{secondaryButtonBgHex}{pair.Value};");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }
        catch (Exception e)
        {
            return "";
        }
    }
}
