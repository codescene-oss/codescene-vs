using Community.VisualStudio.Toolkit;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(OnActiveWindowChangeHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnActiveWindowChangeHandler
{
    const string DocumentKind = "Document";
    private ITextBuffer _currentTextBuffer;
    private Timer _timer;
    private volatile bool _changed;

    public void Handle(Window gotFocus, Window lostFocus)
    {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (gotFocus?.Kind == DocumentKind)
        {
            //var path1 = gotFocus.Document.FullName;
            //gotFocus.DTE.Events.TextEditorEvents.LineChanged += TextEditorEvents_LineChanged;
            _ = SubscribeOnActiveDocumentOnTextChangeEventAsync();
        }

        if (lostFocus?.Kind == DocumentKind)
        {
            _ = UnsubscribeTextChangeEventAsync(lostFocus.Document.FullName);
            //var path2 = Path.GetFileName(lostFocus.Document.Path);
            //lostFocus.DTE.Events.TextEditorEvents.LineChanged -= TextEditorEvents_LineChanged;
        }
    }

    private void TextEditorEvents_LineChanged(TextPoint StartPoint, TextPoint EndPoint, int Hint)
    {
        var e = "";
    }

    async Task UnsubscribeTextChangeEventAsync(string path)
    {
        var docView = await VS.Documents.GetDocumentViewAsync(path);
        if (docView != null)
        {
            docView.TextBuffer.Changed -= TextBuffer_Changed;
        }
    }

    async Task SubscribeOnActiveDocumentOnTextChangeEventAsync()
    {
        var activeDocument = await VS.Documents.GetActiveDocumentViewAsync();
        if (activeDocument != null)
        {
            return;
        }

        _timer?.Dispose();
        _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));

        var p = activeDocument.FilePath;
        activeDocument.TextBuffer.Changed += TextBuffer_Changed;
    }

    private void OnTimerTick(object state)
    {
        if (_changed)
        {
            _changed = false;
            var emir = "";
        }
    }

    private void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
    {
        _changed = true;
    }
}
