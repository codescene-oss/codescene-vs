// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.VS2022.EditorMargin;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Handlers;

[Export(typeof(OnActiveWindowChangeHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnActiveWindowChangeHandler
{
    private const string DocumentKind = "Document";

    [Import]
    private readonly ILogger _logger;

    [Import]
    private readonly ISupportedFileChecker _supportedFileChecker;

    [Import]
    private readonly CodeSceneMarginSettingsManager _marginSettings;

    public void Handle(Window gotFocus, Window lostFocus)
    {
        try
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            HandleFocusedFile(gotFocus);
        }
        catch (Exception e)
        {
            _logger.Error("Could not update margin on file re-focus", e);
        }
    }

    private void HandleFocusedFile(Window focused)
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (focused?.Kind == DocumentKind)
        {
            var doc = focused.Document;
            if (doc == null)
            {
                return;
            }

            string path;
            try
            {
                path = doc.FullName;
            }
            catch (COMException)
            {
                return;
            }

            var isSupportedFile = _supportedFileChecker.IsSupported(path);

            var isTextDocument = false;
            try
            {
                isTextDocument = doc.Object("TextDocument") is TextDocument;
            }
            catch (COMException)
            {
            }

            if (isSupportedFile && isTextDocument)
            {
                _marginSettings.NotifyScoreUpdated();
                return;
            }
        }

        _marginSettings.HideMargin();
    }
}
