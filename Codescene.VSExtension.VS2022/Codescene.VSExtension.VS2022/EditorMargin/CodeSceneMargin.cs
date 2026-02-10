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
using WpfPath = System.Windows.Shapes.Path;

namespace Codescene.VSExtension.VS2022.EditorMargin;

public class CodeSceneMargin : IWpfTextViewMargin
{
    private const string PulseIconData = "M5.76002 2.49999C5.98102 2.50399 6.17302 2.65399 6.23202 2.86699L8.52102 11.19L10.271 5.35599C10.332 5.15399 10.513 5.01099 10.724 4.99999C10.935 4.98899 11.13 5.11199 11.211 5.30699L12.333 7.99899H14C14.276 7.99899 14.5 8.22299 14.5 8.49899C14.5 8.77499 14.276 8.99899 14 8.99899H12C11.798 8.99899 11.616 8.87799 11.538 8.69099L10.826 6.98299L8.97802 13.142C8.91402 13.356 8.71602 13.501 8.49302 13.498C8.27002 13.495 8.07602 13.346 8.01702 13.131L5.71402 4.75699L4.47502 8.64999C4.40902 8.85799 4.21602 8.99799 3.99902 8.99799H1.99902C1.72302 8.99799 1.49902 8.77399 1.49902 8.49799C1.49902 8.22199 1.72302 7.99799 1.99902 7.99799H3.63302L5.27202 2.84599C5.33902 2.63499 5.53702 2.49299 5.75802 2.49799L5.76002 2.49999Z";

    private readonly StackPanel _rootPanel;
    private readonly TextBlock _label;
    private readonly WpfPath _pulseIcon;
    private readonly CodeSceneMarginSettingsManager _settings;

    public CodeSceneMargin(CodeSceneMarginSettingsManager settings)
    {
        _settings = settings;

        _label = new TextBlock
        {
            Text = $"Code Health: N/A",
            Margin = new Thickness(5),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = GetThemedBrush(EnvironmentColors.ToolWindowTextColorKey),
        };

        _pulseIcon = new WpfPath
        {
            Data = Geometry.Parse(PulseIconData),
            Fill = GetThemedBrush(EnvironmentColors.ToolWindowTextColorKey),
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 0, 2, 0),
        };

        _rootPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _pulseIcon, _label },
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

    private static string GetDeltaScore(bool hasDelta, string path)
    {
        if (!hasDelta)
        {
            return null;
        }

        var delta = new DeltaCacheService().GetDeltaForFile(path);
        return delta != null ? $"{delta.OldScore} → {delta.NewScore}" : null;
    }

    private static string GetReviewScore(string code, string path)
    {
        var item = new ReviewCacheService().Get(new ReviewCacheQuery(code, path));
        return item != null ? $"{item.Score}/10" : null;
    }

    private void OnThemeChanged(ThemeChangedEventArgs e)
    {
        var brush = GetThemedBrush(EnvironmentColors.ToolWindowTextColorKey);
        _label.Foreground = brush;
        _pulseIcon.Fill = brush;
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

            if (!show || path == null)
            {
                return;
            }

            var score = GetDeltaScore(_settings.HasDelta, path) ?? GetReviewScore(code, path) ?? "N/A";
            _label.Text = $"Code Health: {score} ({Path.GetFileName(path)})";
        });
    }
}
