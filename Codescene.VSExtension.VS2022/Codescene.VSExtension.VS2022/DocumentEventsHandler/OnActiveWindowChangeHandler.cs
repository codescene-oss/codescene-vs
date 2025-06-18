using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.VS2022.EditorMargin;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
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
        _logger.Info($"OnActiveWindowChangeHandler. gotFocus: {gotFocus?.Document?.FullName}; lostFocus: {lostFocus?.Document?.FullName}");

        ThreadHelper.ThrowIfNotOnUIThread();
        if (gotFocus?.Kind == DocumentKind)
        {
            var doc = gotFocus.Document;
            var path = gotFocus.Document.FullName;
            var isSupportedFile = _supportedFileChecker.IsSupported(path);

            if (isSupportedFile && doc.Object("TextDocument") is TextDocument textDoc)
            {
                // get latest content for file currently in focus and update margin
                var editPoint = textDoc.StartPoint.CreateEditPoint();
                string content = editPoint.GetText(textDoc.EndPoint);

                _logger.Info($"Current content length: {content.Length}");
                _marginSettings.UpdateMarginData(path, content);
            }
        }
    }
}
