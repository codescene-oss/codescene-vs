// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Util;

public static class DocumentNavigator
{
    public static async Task OpenFileAndGoToLineAsync(string filePath, int lineNumber, ILogger logger)
    {
        try
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var dte = await ServiceProvider.GetGlobalServiceAsync(typeof(DTE)) as DTE2;
            var window = dte?.ItemOperations.OpenFile(filePath);

            if (window == null)
            {
                return;
            }

            window.Visible = true;

            var doc = dte.ActiveDocument;
            var textSelection = (TextSelection)doc.Selection;
            textSelection.GotoLine(lineNumber, Select: false);
        }
        catch (Exception e)
        {
            logger.Error($"Unable to open file and focus on line {lineNumber} ", e);
        }
    }
}
