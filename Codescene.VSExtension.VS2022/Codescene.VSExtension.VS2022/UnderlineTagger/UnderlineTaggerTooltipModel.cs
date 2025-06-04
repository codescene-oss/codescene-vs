using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
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
    [Export(typeof(UnderlineTaggerTooltipModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class UnderlineTaggerTooltipModel
    {
        public string Category { get; set; }
        public string Details { get; set; }
        public string Path { get; set; }
        public CodeSmellRange Range { get; set; }
        public string FunctionName { get; set; }

        public ICommand YourCommand { get; }
        public int WindowId { get; set; }

        private readonly ShowDocumentationHandler _showDocumentationHandler;

        [ImportingConstructor]
        public UnderlineTaggerTooltipModel(ShowDocumentationHandler showDocumentationHandler)
        {
            _showDocumentationHandler = showDocumentationHandler;
            YourCommand = new RelayCommand(ExecuteYourCommand);
        }

        public CodeSmellTooltipModel CommandParameter => new CodeSmellTooltipModel(
            Category, Details, Path, FunctionName, new RangeModel(
                Range.StartLine,
                Range.EndLine,
                Range.StartColumn,
                Range.EndColumn
            ));

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
                            new CodeSmellRange(
                                cmdParam.Range.StartLine,
                                cmdParam.Range.EndLine,
                                cmdParam.Range.StartColumn,
                                cmdParam.Range.EndColumn
                            )
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
