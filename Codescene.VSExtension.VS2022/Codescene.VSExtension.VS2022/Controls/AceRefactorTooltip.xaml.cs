using Codescene.VSExtension.Core.Models.Cli.Refactor;
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

            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var compositionService = componentModel.DefaultCompositionService;
            compositionService.SatisfyImportsOnce(this);

            _viewModel.Path = aceRefactorTooltipParams.Path;
            _viewModel.RefactorableFunction = aceRefactorTooltipParams.RefactorableFunction;

            DataContext = _viewModel;
        }
    }
}