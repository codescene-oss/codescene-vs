using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Models.ReviewModels;
using Codescene.VSExtension.VS2022.DocumentEventsHandler;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
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
using static Codescene.VSExtension.CodeLensProvider.Providers.Base.Constants;

namespace Codescene.VSExtension.VS2022.EditorMargin;

public class CodeSceneMargin : IWpfTextViewMargin
{
    private readonly StackPanel _rootPanel;
    private readonly TextBlock _label;
    private readonly CodeSceneMarginSettingsManager _settings;
    private readonly IReviewedFilesCacheHandler _cache;

    public CodeSceneMargin(CodeSceneMarginSettingsManager settings, IReviewedFilesCacheHandler cache)
    {
        this._settings = settings;
        this._cache = cache;

        _label = new TextBlock
        {
            Text = $"{Titles.CODESCENE} Code Health Score: N/A",
            Margin = new Thickness(5),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = GetThemedBrush(EnvironmentColors.ToolWindowTextColorKey)
        };

        _rootPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal,
            Children = { _label }
        };
        
        _settings.ScoreUpdated += UpdateUI;
        VSColorTheme.ThemeChanged += OnThemeChanged;

        UpdateUI();
    }

    private void OnThemeChanged(ThemeChangedEventArgs e)
    {
        _label.Foreground = GetThemedBrush(EnvironmentColors.ToolWindowTextColorKey); 
    }

    private SolidColorBrush GetThemedBrush(ThemeResourceKey key)
    {
        var drawingColor = VSColorTheme.GetThemedColor(key);
        var mediaColor = Color.FromArgb(drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);
        return new SolidColorBrush(mediaColor);
    }


    private void UpdateUI()
    {
        _ = _rootPanel.Dispatcher.Invoke(async () =>
        {
            bool show = _settings.HasScore;
            _rootPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                var activeDocument = _settings.FileInFocus;
                if (activeDocument != null && _cache.Exists(activeDocument))
                {
                    FileReviewModel cachedReview = _cache.Get(activeDocument);
                    _label.Text = $"{Titles.CODESCENE} Code Health Score: {cachedReview.Score} ({Path.GetFileName(cachedReview.FilePath)})";
                }

            }
        });
    }

    public FrameworkElement VisualElement => _rootPanel;

    public bool Enabled => true;

    public double MarginSize => _rootPanel.ActualHeight;

    public string MarginName => this.GetType().Name;

    public void Dispose() 
    {
        _settings.ScoreUpdated -= UpdateUI;
    }

    public ITextViewMargin GetTextViewMargin(string marginName)
    {
        return this;
    }

    public bool IsDisposed => false;
}
