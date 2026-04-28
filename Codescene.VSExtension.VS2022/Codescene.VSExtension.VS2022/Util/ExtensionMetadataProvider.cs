// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codescene.VSExtension.VS2022.Util;

[Export(typeof(IExtensionMetadataProvider))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class VsExtensionMetadataProvider : IExtensionMetadataProvider
{
    private string _cachedEditorVersion;

    public string GetVersion() => Vsix.Version;

    public string GetDisplayName() => Vsix.Name;

    public string GetDescription() => Vsix.Description;

    public string GetPublisher() => Vsix.Author;

    public string GetEditorVersion()
    {
        if (_cachedEditorVersion != null)
        {
            return _cachedEditorVersion;
        }

        var editorVersion = ThreadHelper.JoinableTaskFactory.Run(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (Package.GetGlobalService(typeof(SVsShell)) is not IVsShell vsShell)
            {
                return string.Empty;
            }

            if (vsShell.GetProperty((int)__VSSPROPID5.VSSPROPID_ReleaseVersion, out var raw) != VSConstants.S_OK)
            {
                return string.Empty;
            }

            return TrimToThreeParts(raw as string ?? string.Empty);
        });

        if (!string.IsNullOrEmpty(editorVersion))
        {
            _cachedEditorVersion = editorVersion;
        }

        return editorVersion;
    }

    private static string TrimToThreeParts(string version)
    {
        if (string.IsNullOrEmpty(version))
        {
            return string.Empty;
        }

        var parts = version.Split('.');
        return parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : version;
    }
}
