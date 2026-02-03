// Copyright (c) CodeScene. All rights reserved.

using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codescene.VSExtension.VS2022.Application.ErrorListWindowHandler;

public static class HierarchyHelper
{
    /// <summary>
    /// Returns an IVsHierarchy for the file path if that file is contained in a loaded project,
    /// or null otherwise.
    /// </summary>
    public static IVsHierarchy GetHierarchyFromFile(string filePath)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        IServiceProvider serviceProvider = VS2022Package.Instance;

        if (string.IsNullOrEmpty(filePath) || serviceProvider == null)
        {
            return null;
        }

        // Get the IVsUIShellOpenDocument service
        var shellOpenDoc = serviceProvider.GetService(typeof(SVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
        if (shellOpenDoc == null)
        {
            return null;
        }

        // Query if the file belongs to a project
        int hr = shellOpenDoc.IsDocumentInAProject(filePath, out IVsUIHierarchy hierarchy, out _, out _, out int found);

        var succeeded = ErrorHandler.Succeeded(hr) && found != 0 && hierarchy != null;
        if (succeeded)
        {
            return hierarchy;
        }

        return null;
    }
}
