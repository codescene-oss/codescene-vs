using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.VS2022.EditorMargin;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnActiveWindowChangeHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnActiveWindowChangeHandler
{
    const string DocumentKind = "Document";

    [Import]
    private readonly ILogger _logger;

    [Import]
    private readonly ISupportedFileChecker _supportedFileChecker;

    [Import]
    private readonly CodeSceneMarginSettingsManager _marginSettings;

    public void Handle(Window gotFocus, Window lostFocus)
    {
        _logger.Debug($"File got focus: {gotFocus?.Document?.FullName}; File lost focus: {lostFocus?.Document?.FullName}");

        try
        {
            HandleFocusedFile(gotFocus);
        }
        catch (Exception e)
        {
            _logger.Error($"Could not update margin on file re-focus. Error message: {e.Message}");
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

            if (isSupportedFile && doc.Object("TextDocument") is TextDocument textDoc)
            {
                // Get latest content for file currently in focus and update margin
                var editPoint = textDoc.StartPoint.CreateEditPoint();
                string content = editPoint.GetText(textDoc.EndPoint);

                _marginSettings.UpdateMarginData(path, content);
                return;
            }
        }

        _marginSettings.HideMargin(); // For unsupported files, or non-document (code) files
    }
}
