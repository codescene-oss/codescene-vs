﻿using Codescene.VSExtension.CodeLensProvider.Providers.Base;
using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.ErrorListWindowHandler;
using Codescene.VSExtension.VS2022.EditorMargin;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
using System.ComponentModel.Composition;
using System.IO;
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
    private readonly ILogger _logger;

    [Import]
    private readonly ICodeReviewer _reviewer;

    [Import]
    private readonly IErrorListWindowHandler _errorListWindowHandler;

    [Import]
    private readonly ISupportedFileChecker _supportedFileChecker;

    [Import]
    private readonly CodeSceneMarginSettingsManager _marginSettings;

    public async Task UnsubscribeAsync(string path)
    {
        var docView = await VS.Documents.GetDocumentViewAsync(path);
        if (docView != null)
        {
            docView.TextBuffer.Changed -= TextBuffer_Changed;
        }
    }

    TimeSpan TimerInterval { get { return TimeSpan.FromMilliseconds(Constants.Utils.TEXT_CHANGE_CHECK_INTERVAL_MILISECONDS); } }

    public async Task SubscribeAsync(string path)
    {
        var activeDocument = await VS.Documents.GetDocumentViewAsync(path);

        if (activeDocument?.FilePath == null) return;

        var extension = Path.GetExtension(activeDocument.FilePath);
        bool fileSupported = _supportedFileChecker.IsSupported(extension);

        if (activeDocument != null && fileSupported)
        {
            _timer?.Dispose();
            _timer = new Timer((state) =>
            {
                //On timer tick
                OnTimerElapsed(activeDocument.FilePath);
            }, null, TimerInterval, TimerInterval);

            activeDocument.TextBuffer.Changed += TextBuffer_Changed;
        }
    }

    private void OnTimerElapsed(string path)
    {
        if (_changed)
        {
            _changed = false;

            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                // Now safe to use VS APIs that require the UI thread:
                var newContent = _buffer.CurrentSnapshot.GetText();
                _reviewer.UseContentOnlyType(newContent);
                var review = _reviewer.Review(path, invalidateCache: true);
                _marginSettings.UpdateMarginData(path);
                    
                _errorListWindowHandler.Handle(review);

                // Also, call RefreshCodeLensAsync on the UI thread
                //CodesceneCodelensCallbackService.RefreshCodeLensAsync().FireAndForget();
            });
        }
    }

    private void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
    {
        _buffer = (ITextBuffer)sender;
        _changed = true;
    }
}
