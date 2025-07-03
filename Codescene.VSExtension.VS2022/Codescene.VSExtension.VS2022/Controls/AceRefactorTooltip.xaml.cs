using Codescene.VSExtension.VS2022.Commands;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Codescene.VSExtension.VS2022.UnderlineTagger;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Windows.Input;

namespace Codescene.VSExtension.VS2022.Controls
{
    public class AceRefactorTooltipParams(string path)
    {
        public string Path { get; set; } = path;
    }

    public partial class AceRefactorTooltip : UserControl
    {
        [Import]
        private AceRefactorTooltipModel _viewModel { get; set; }

        public AceRefactorTooltip(AceRefactorTooltipParams aceRefactorTooltipParams)
        {
            InitializeComponent();

            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var compositionService = componentModel.DefaultCompositionService;
            compositionService.SatisfyImportsOnce(this);

            _viewModel.Path = aceRefactorTooltipParams.Path;

            DataContext = _viewModel;
        }
    }
}