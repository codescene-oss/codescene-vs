// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
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
            var path = focused.Document.FullName;
            var isSupportedFile = _supportedFileChecker.IsSupported(path);

            if (isSupportedFile && doc.Object("TextDocument") is TextDocument)
            {
                _marginSettings.NotifyScoreUpdated();
                return;
            }
        }

        _marginSettings.HideMargin();
    }
}
