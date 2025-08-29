using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.VS2022.CodeLens;
using Codescene.VSExtension.VS2022.Commands;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Codescene.VSExtension.VS2022.Util;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;
using static Codescene.VSExtension.VS2022.Util.LogHelper;

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
        public string FunctionName { get; set; }

        public ICommand YourCommand { get; }
        public int WindowId { get; set; }

        private readonly ShowDocumentationHandler _showDocumentationHandler;

        [ImportingConstructor]
        public ReviewResultTaggerTooltipModel(ShowDocumentationHandler showDocumentationHandler)
        {
            _showDocumentationHandler = showDocumentationHandler;
            YourCommand = new RelayCommand(ExecuteYourCommand);
        }

        public CodeSmellTooltipModel CommandParameter => new CodeSmellTooltipModel(
            Category, Details, Path, FunctionName, new CodeSmellRangeModel(
                Range.StartLine,
                Range.EndLine,
                Range.StartColumn,
                Range.EndColumn
            ));

        //Bindings are defined in UnderlineTaggerTooltip.xaml
        private void ExecuteYourCommand(object parameter)
        {
            try
            {
                var cmdParam = parameter as CodeSmellTooltipModel;
                if (cmdParam != null && ToolWindowRegistry.CategoryToIdMap.TryGetValue(cmdParam.Category, out int toolWindowId))
                {
                    _showDocumentationHandler.HandleAsync(
                        new ShowDocumentationModel(
                            cmdParam.Path,
                            cmdParam.Category,
                            cmdParam.FunctionName,
                            cmdParam.Range
                        )
                    ).FireAndForget();
                }
            }
            catch (Exception e)
            {
                LogAsync("Unable to handle tagger action.", LogLevel.Error, e).FireAndForget();
            }
        }
    }
}
