using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.VS2022.CodeLens;
using Codescene.VSExtension.VS2022.Commands;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace Codescene.VSExtension.VS2022.UnderlineTagger
{
    [Export(typeof(ReviewResultTaggerTooltipModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class ReviewResultTaggerTooltipModel
    {
        public string Category { get; set; }
        public string Details { get; set; }
        public string Path { get; set; }
        public CodeRangeModel Range { get; set; }
        public CodeRangeModel FunctionRange { get; set; }
        public string FunctionName { get; set; }

        public ICommand YourCommand { get; }

        private readonly ShowDocumentationHandler _showDocumentationHandler;

        [ImportingConstructor]
        public ReviewResultTaggerTooltipModel(ShowDocumentationHandler showDocumentationHandler)
        {
            _showDocumentationHandler = showDocumentationHandler;
            YourCommand = new RelayCommand(ExecuteYourCommand);
        }

        public CodeSmellTooltipModel CommandParameter => new()
        {
            Category = Category,
            Details = Details,
            Path = Path,
            FunctionName = FunctionName,
            Range = new CodeRangeModel(
                Range.StartLine,
                Range.EndLine,
                Range.StartColumn,
                Range.EndColumn
            ),
            FunctionRange = FunctionRange is null
            ? null
            : new CodeRangeModel(
                FunctionRange.StartLine,
                FunctionRange.EndLine,
                FunctionRange.StartColumn,
                FunctionRange.EndColumn
            )
        };

        //Bindings are defined in UnderlineTaggerTooltip.xaml
        private async void ExecuteYourCommand(object parameter)
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();

            try
            {
                var cmdParam = parameter as CodeSmellTooltipModel;

                if (cmdParam != null && ToolWindowRegistry.CategoryToIdMap.TryGetValue(cmdParam.Category, out int toolWindowId))
                {
                    var fnToRefactor = await AceUtils.GetRefactorableFunctionAsync(new GetRefactorableFunctionsModel
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
                            cmdParam.FunctionRange
                        ),
                        fnToRefactor
                    );
                }
            }
            catch (Exception e)
            {
                logger.Error("Unable to handle tagger action.", e);
            }
        }
    }
}
