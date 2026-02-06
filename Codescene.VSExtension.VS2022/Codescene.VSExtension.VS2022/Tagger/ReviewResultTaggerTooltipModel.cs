// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Input;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.VS2022.Commands;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;

namespace Codescene.VSExtension.VS2022.UnderlineTagger
{
    [Export(typeof(ReviewResultTaggerTooltipModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class ReviewResultTaggerTooltipModel
    {
        private readonly ShowDocumentationHandler _showDocumentationHandler;

        [ImportingConstructor]
        public ReviewResultTaggerTooltipModel(ShowDocumentationHandler showDocumentationHandler)
        {
            _showDocumentationHandler = showDocumentationHandler;
            YourCommand = new RelayCommand(ExecuteYourCommand);
        }

        public string Category { get; set; }

        public string Details { get; set; }

        public string Path { get; set; }

        public CodeRangeModel Range { get; set; }

        public CodeRangeModel FunctionRange { get; set; }

        public string FunctionName { get; set; }

        public ICommand YourCommand { get; }

        public CodeSmellTooltipModel CommandParameter => new CodeSmellTooltipModel()
        {
            Category = Category,
            Details = Details,
            Path = Path,
            FunctionName = FunctionName,
            Range = new CodeRangeModel(
                Range.StartLine,
                Range.EndLine,
                Range.StartColumn,
                Range.EndColumn),
            FunctionRange = FunctionRange is null
            ? null
            : new CodeRangeModel(
                FunctionRange.StartLine,
                FunctionRange.EndLine,
                FunctionRange.StartColumn,
                FunctionRange.EndColumn),
        };

        // Bindings are defined in UnderlineTaggerTooltip.xaml
#pragma warning disable VSTHRD100 // Avoid async void methods - RelayCommand requires void return type
        private async void ExecuteYourCommand(object parameter)
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();

            try
            {
                var cmdParam = parameter as CodeSmellTooltipModel;

                if (cmdParam != null && DocumentationMappings.DocNameMap.Values.Contains(cmdParam.Category))
                {
                    var fnToRefactor = await AceUtils.GetRefactorableFunctionCodeSmellAsync(new GetRefactorableFunctionsModel
                    {
                        Path = cmdParam.Path,
                        Range = cmdParam.Range,
                        Details = cmdParam.Details,
                        Category = cmdParam.Category,
                        FunctionName = cmdParam.FunctionName,
                        FunctionRange = cmdParam.FunctionRange,
                    });

                    await _showDocumentationHandler.HandleAsync(
                        new ShowDocumentationModel(
                            cmdParam.Path,
                            cmdParam.Category,
                            cmdParam.FunctionName,
                            cmdParam.FunctionRange),
                        fnToRefactor);
                }
            }
            catch (Exception e)
            {
                logger.Error("Unable to handle tagger action.", e);
            }
        }
    }
}
