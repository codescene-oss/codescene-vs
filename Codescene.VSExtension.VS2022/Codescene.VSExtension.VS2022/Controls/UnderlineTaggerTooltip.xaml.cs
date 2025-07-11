﻿using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.VS2022.UnderlineTagger;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.Windows.Controls;

namespace Codescene.VSExtension.VS2022.Controls
{
    public class UnderlineTaggerTooltipParams(string category, string details, string path, CodeSmellRangeModel range, string functionName)
    {
        public string Category { get; } = category;
        public string Details { get; } = details;
        public string Path { get; } = path;
        public CodeSmellRangeModel Range { get; } = range;
        public string FunctionName { get; } = functionName;
    }

    public partial class UnderlineTaggerTooltip : UserControl
    {
        [Import]
        private ReviewResultTaggerTooltipModel _viewModel { get; set; }

        public UnderlineTaggerTooltip(UnderlineTaggerTooltipParams tooltipParams)
        {
            InitializeComponent();

            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var compositionService = componentModel.DefaultCompositionService;
            compositionService.SatisfyImportsOnce(this);

            _viewModel.Category = tooltipParams.Category;
            _viewModel.Details = tooltipParams.Details;
            _viewModel.Path = tooltipParams.Path;
            _viewModel.Range = tooltipParams.Range;
            _viewModel.FunctionName = tooltipParams.FunctionName;

            DataContext = _viewModel;
        }
    }
}
