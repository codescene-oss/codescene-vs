// Copyright (c) CodeScene. All rights reserved.

using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Models.Cache.Review;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using static Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.VS2022.EditorMargin;

public class CodeSceneMargin : IWpfTextViewMargin
{
    private readonly StackPanel _rootPanel;
    private readonly TextBlock _label;
    private readonly CodeSceneMarginSettingsManager _settings;

    public CodeSceneMargin(CodeSceneMarginSettingsManager settings)
    {
        _settings = settings;

        _label = new TextBlock
        {
            Text = $"{Titles.CODESCENE} Code Health Score: N/A",
            Margin = new Thickness(5),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = GetThemedBrush(EnvironmentColors.ToolWindowTextColorKey),
        };

        _rootPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _label },
        };

        _settings.ScoreUpdated += UpdateUIAsync;
        VSColorTheme.ThemeChanged += OnThemeChanged;

        UpdateUIAsync().FireAndForget();
    }

    public FrameworkElement VisualElement => _rootPanel;

    public bool Enabled => true;

    public double MarginSize => _rootPanel.ActualHeight;

    public string MarginName => this.GetType().Name;

    public bool IsDisposed => false;

    public void Dispose()
    {
        _settings.ScoreUpdated -= UpdateUIAsync;
    }

    public ITextViewMargin GetTextViewMargin(string marginName)
    {
        return this;
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

    private async Task UpdateUIAsync()
    {
        await ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(alwaysYield: true);
            bool show = _settings.HasScore;
            _rootPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

            var path = _settings.FileInFocus;
            var code = _settings.FileInFocusContent;

            if (show && path != null)
            {
                var cache = new ReviewCacheService();
                var item = cache.Get(new ReviewCacheQuery(code, path));

                if (item != null)
                {
                    string score = item.Score.ToString();
                    if (score == "0")
                    {
                        score = "N/A";
                    }

                    _label.Text = $"{Titles.CODESCENE} Code Health Score: {score} ({Path.GetFileName(path)})";
                }
            }
        });
    }
}
