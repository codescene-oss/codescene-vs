using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static Codescene.VSExtension.Core.Application.Services.Util.Constants;

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
        _rootPanel.Dispatcher.Invoke(() =>
        {
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
                    if (score == "0") score = "N/A";

                    _label.Text = $"{Titles.CODESCENE} Code Health Score: {score} ({Path.GetFileName(path)})";
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
