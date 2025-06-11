using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.VS2022.DocumentEventsHandler;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Codescene.VSExtension.VS2022.EditorMargin;

public class CodeSceneMargin : IWpfTextViewMargin
{
    private readonly StackPanel _rootPanel;
    private readonly TextBlock _label;
    private readonly OnDocumentSavedHandler _activeDocumentTextChangeHandler;
    private readonly IReviewedFilesCacheHandler _cache;

    public CodeSceneMargin(
        OnDocumentSavedHandler activeDocumentTextChangeHandler,
        IReviewedFilesCacheHandler cache)
    {
        this._activeDocumentTextChangeHandler = activeDocumentTextChangeHandler;
        this._cache = cache;

        _label = new TextBlock
        {
            Text = $"CodeScene Code Health Score: N/A",
            Margin = new Thickness(5),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White
        };

        _rootPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal,
            Children = { _label }
        };
        
        _activeDocumentTextChangeHandler.ScoreUpdated += UpdateUI;

        UpdateUI();
    }

    private void UpdateUI()
    {
        _rootPanel.Dispatcher.Invoke(async () =>
        {
            bool show = _activeDocumentTextChangeHandler.HasScore;
            _rootPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                var activeDocument = await VS.Documents.GetActiveDocumentViewAsync();
                if (activeDocument != null && _cache.Exists(activeDocument.FilePath))
                {
                    FileReviewModel cachedReview = _cache.Get(activeDocument.FilePath);
                    _label.Text = $"CodeScene Code Health Score: {cachedReview.Score} ({cachedReview.FilePath})";
                }

            }
        });
    }

    public FrameworkElement VisualElement => _rootPanel;

    public bool Enabled => true;

    public double MarginSize => _rootPanel.ActualHeight;

    public string MarginName => "CodeSceneMargin";

    public void Dispose() 
    {
        _activeDocumentTextChangeHandler.ScoreUpdated -= UpdateUI;
    }

    public ITextViewMargin GetTextViewMargin(string marginName)
    {
        return this;
    }

    public bool IsDisposed => false;
}
