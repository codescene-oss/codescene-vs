using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace Codescene.VSExtension.VS2022.Application.ErrorListWindowHandler;
public static class HierarchyHelper
{
    /// <summary>
    /// Returns an IVsHierarchy for the file path if that file is contained in a loaded project,
    /// or null otherwise.
    /// </summary>
    public static IVsHierarchy GetHierarchyFromFile(IServiceProvider serviceProvider, string filePath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (string.IsNullOrEmpty(filePath))
            return null;

        // Get the IVsUIShellOpenDocument service
        var shellOpenDoc = serviceProvider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
        if (shellOpenDoc == null)
            return null;

        // Query if the file belongs to a project
        int hr = shellOpenDoc.IsDocumentInAProject(filePath, out IVsUIHierarchy hierarchy, out _, out _, out int found);

        if (ErrorHandler.Succeeded(hr) && found != 0 && hierarchy != null)
        {
            return hierarchy;
        }
        return null;
    }
}

