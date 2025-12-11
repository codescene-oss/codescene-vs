using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Windows.Media;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.VS2022.Commands;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Codescene.VSExtension.VS2022.UnderlineTagger;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace Codescene.VSExtension.VS2022.Controls
{
    public class AceRefactorTooltipParams(string path, FnToRefactorModel refactorableFunction)
    {
        public string Path { get; set; } = path;
        public FnToRefactorModel RefactorableFunction { get; set; } = refactorableFunction;
    }

    public partial class AceRefactorTooltip : UserControl
    {
        [Import]
        private AceRefactorTooltipModel _viewModel { get; set; }

        public AceRefactorTooltip(AceRefactorTooltipParams aceRefactorTooltipParams)
        {
            InitializeComponent();
            ApplyThemeAwareTextColor();

            //handle theme changes at runtime
            VSColorTheme.ThemeChanged += OnThemeChanged;

            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var compositionService = componentModel.DefaultCompositionService;
            compositionService.SatisfyImportsOnce(this);

            _viewModel.Path = aceRefactorTooltipParams.Path;
            _viewModel.RefactorableFunction = aceRefactorTooltipParams.RefactorableFunction;

            DataContext = _viewModel;
        }

        private void OnThemeChanged(ThemeChangedEventArgs e)
        {
            ApplyThemeAwareTextColor();
        }

        private void ApplyThemeAwareTextColor()
        {
            var isDarkTheme = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey).GetBrightness() < 0.5;

            var hyperlinkBrush = new SolidColorBrush(
                isDarkTheme ? Colors.DeepSkyBlue : Colors.Blue);

            hyperlinkContrast.Foreground = hyperlinkBrush;
        }
    }
}