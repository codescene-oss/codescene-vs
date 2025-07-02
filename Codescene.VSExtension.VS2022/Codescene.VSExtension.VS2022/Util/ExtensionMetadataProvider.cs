namespace Codescene.VSExtension.VS2022.Util;

public static class ExtensionMetadataProvider
{
    public static string GetVersion() => Vsix.Version;
    public static string GetDisplayName() => Vsix.Name;
    public static string GetDescription() => Vsix.Description;
    public static string GetPublisher() => Vsix.Author;
}

