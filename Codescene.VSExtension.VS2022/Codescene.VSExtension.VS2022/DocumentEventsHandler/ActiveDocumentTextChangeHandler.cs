using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using Codescene.VSExtension.VS2022.CodeLens;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.DocumentEventsHandler;

[Export(typeof(ActiveDocumentTextChangeHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class ActiveDocumentTextChangeHandler
{
    private ITextBuffer _buffer;
    private Timer _timer;
    private volatile bool _changed;

    [Import]
    private readonly ICodeReviewer _reviewer;

    [Import]
    private readonly IErrorListWindowHandler _errorListWindowHandler;

    public async Task UnsubscribeAsync(string path)
    {
        var docView = await VS.Documents.GetDocumentViewAsync(path);
        if (docView != null)
        {
            docView.TextBuffer.Changed -= TextBuffer_Changed;
        }
    }

    public async Task SubscribeAsync()
    {
        var activeDocument = await VS.Documents.GetActiveDocumentViewAsync();
        if (activeDocument != null)
        {
            _timer?.Dispose();
            _timer = new Timer((state) =>
            {
                OnTimerTick(state, activeDocument.FilePath);
            }, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));

            var p = activeDocument.FilePath;
            activeDocument.TextBuffer.Changed += TextBuffer_Changed;
        }
    }

    private void OnTimerTick(object state, string path)
    {
        if (_changed)
        {
            _changed = false;

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Now safe to use VS APIs that require the UI thread:
                var newContent = _buffer.CurrentSnapshot.GetText();
                var review = _reviewer.ReviewContent(path, newContent);
                _errorListWindowHandler.Handle(review);

                // Also, call RefreshCodeLensAsync on the UI thread
                await CodesceneCodelensCallbackService.RefreshCodeLensAsync();
            });
        }
    }

    private void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
    {
        _buffer = (ITextBuffer)sender;
        _changed = true;
    }
}
