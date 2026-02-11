// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Windows.Media;
using Codescene.VSExtension.VS2022.Tagger;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Controls
{
    public partial class UnderlineTaggerTooltip : UserControl
    {
        public UnderlineTaggerTooltip(UnderlineTaggerTooltipParams tooltipParams)
        {
            InitializeComponent();
            ApplyThemeAwareTextColor();

            // handle theme changes at runtime
            VSColorTheme.ThemeChanged += OnThemeChanged;

            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var compositionService = componentModel.DefaultCompositionService;
            compositionService.SatisfyImportsOnce(this);

            ViewModel.Category = tooltipParams.Category;
            ViewModel.Details = tooltipParams.Details;
            ViewModel.Path = tooltipParams.Path;
            ViewModel.Range = tooltipParams.Range;
            ViewModel.FunctionRange = tooltipParams.FunctionRange;
            ViewModel.FunctionName = tooltipParams.FunctionName;

            DataContext = ViewModel;
        }

        [Import]
        private ReviewResultTaggerTooltipModel ViewModel { get; set; }

        private void OnThemeChanged(ThemeChangedEventArgs e)
        {
            ApplyThemeAwareTextColor();
        }

        private void ApplyThemeAwareTextColor()
        {
            var drawingColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolTipTextColorKey);

            var mediaColor = Color.FromArgb(
                drawingColor.A, drawingColor.R, drawingColor.G, drawingColor.B);

            ThemedTextBlock.Foreground = new SolidColorBrush(mediaColor);

            var isDarkTheme = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey).GetBrightness() < 0.5;

            var hyperlinkBrush = new SolidColorBrush(
                isDarkTheme ? Colors.DeepSkyBlue : Colors.Blue);

            hyperlinkContrast.Foreground = hyperlinkBrush;
        }
    }
}
