using Codescene.VSExtension.Core.Application.Services.Cli;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.IO;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnActiveWindowChangeHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnActiveWindowChangeHandler
{
    const string DocumentKind = "Document";

    [Import]
    private readonly ActiveDocumentTextChangeHandler _documentHandler;

    [Import]
    private readonly ISupportedFileChecker _supportedFileChecker;

    public void Handle(Window gotFocus, Window lostFocus)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (gotFocus?.Kind == DocumentKind)
        {
            var extension = Path.GetExtension(gotFocus.Document.Name);
            if (_supportedFileChecker.IsNotSupported(extension))
            {
                return;
            }

            _ = _documentHandler.SubscribeAsync();
        }

        if (lostFocus?.Kind == DocumentKind)
        {
            _ = _documentHandler.UnsubscribeAsync(lostFocus.Document.FullName);
        }
    }
}
