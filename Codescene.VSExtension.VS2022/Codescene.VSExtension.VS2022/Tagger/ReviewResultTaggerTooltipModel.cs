using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.VS2022.CodeLens;
using Codescene.VSExtension.VS2022.Commands;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
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
        public CodeSmellRangeModel Range { get; set; }
        public CodeSmellRangeModel FunctionRange { get; set; }
        public string FunctionName { get; set; }

        public ICommand YourCommand { get; }

        private readonly ShowDocumentationHandler _showDocumentationHandler;

        [ImportingConstructor]
        public ReviewResultTaggerTooltipModel(ShowDocumentationHandler showDocumentationHandler)
        {
            _showDocumentationHandler = showDocumentationHandler;
            YourCommand = new RelayCommand(ExecuteYourCommand);
        }

        public CodeSmellTooltipModel CommandParameter => new CodeSmellTooltipModel(
            Category,
            Details,
            Path,
            FunctionName,
            new CodeSmellRangeModel(
                Range.StartLine,
                Range.EndLine,
                Range.StartColumn,
                Range.EndColumn
            ),
            FunctionRange is null
                ? null
                : new CodeSmellRangeModel(
                    FunctionRange.StartLine,
                    FunctionRange.EndLine,
                    FunctionRange.StartColumn,
                    FunctionRange.EndColumn
                )
        );

        //Bindings are defined in UnderlineTaggerTooltip.xaml
        private async void ExecuteYourCommand(object parameter)
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();

            try
            {
                var cmdParam = parameter as CodeSmellTooltipModel;
                if (cmdParam != null && ToolWindowRegistry.CategoryToIdMap.TryGetValue(cmdParam.Category, out int toolWindowId))
                {
                    await _showDocumentationHandler.HandleAsync(
                        new ShowDocumentationModel(
                            cmdParam.Path,
                            cmdParam.Category,
                            cmdParam.FunctionName,
                            cmdParam.FunctionRange
                        )
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
