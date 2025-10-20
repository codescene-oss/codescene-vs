namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
internal class SkipShowDiffHelper
{
    public const string SHOW_DIFF_FOLDER = "\\ShowDiff\\";
    public static bool PathContainsShowDiffFolder(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        return path.Contains(SHOW_DIFF_FOLDER);
    }
}
