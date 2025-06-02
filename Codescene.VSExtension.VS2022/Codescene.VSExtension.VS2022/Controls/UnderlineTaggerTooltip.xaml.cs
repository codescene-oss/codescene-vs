using Codescene.VSExtension.VS2022.UnderlineTagger;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.Windows.Controls;

namespace Codescene.VSExtension.VS2022.Controls
{

    public partial class UnderlineTaggerTooltip : UserControl
    {
        [Import]
        private UnderlineTaggerTooltipModel _viewModel { get; set; }

        public UnderlineTaggerTooltip(string category, string details)
        {
            InitializeComponent();

            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var compositionService = componentModel.DefaultCompositionService;
            compositionService.SatisfyImportsOnce(this);

            _viewModel.Category = category;
            _viewModel.Details = details;

            DataContext = _viewModel;
        }
    }
}
