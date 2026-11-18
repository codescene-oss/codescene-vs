// Copyright (c) CodeScene. All rights reserved.

using System.Text;

namespace Codescene.VSExtension.Core.Util;

public static class ThemeStatusColors
{
    public static bool IsLightBackground(byte r, byte g, byte b)
    {
        var brightness = ((0.299 * r) + (0.587 * g) + (0.114 * b)) / 255;
        return brightness >= 0.5;
    }

    public static string IssueHex(byte r, byte g, byte b)
    {
        return IsLightBackground(r, g, b) ? "A15C00" : "EE9D28";
    }

    public static void AppendStatusVariables(StringBuilder sb, string successHex, string warningHex, string issueHex)
    {
        sb.AppendLine($"  --cs-theme-success: #{successHex};");
        sb.AppendLine($"  --cs-theme-warning: #{warningHex};");
        sb.AppendLine($"  --cs-theme-issue: #{issueHex};");
    }
}
