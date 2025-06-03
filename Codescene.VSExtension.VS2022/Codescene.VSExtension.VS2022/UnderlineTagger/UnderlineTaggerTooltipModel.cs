using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.VS2022.CodeLens;
using Codescene.VSExtension.VS2022.Commands;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Community.VisualStudio.Toolkit;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;
using static Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers.ShowDocumentationParams;

namespace Codescene.VSExtension.VS2022.UnderlineTagger
{
    public class TooltipCommandParameter
    {
        public string Category { get; set; }
        public string Details { get; set; }
        public string Path { get; set; }
        public string FunctionName { get; set; }
        public CodeSmellRange Range { get; set; }
    }

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

        public TooltipCommandParameter CommandParameter => new TooltipCommandParameter
        {
            Category = Category,
            Details = Details,
            Path = Path,
            FunctionName = FunctionName,
            Range = Range
        };

        //Bindings are defined in UnderlineTaggerTooltip.xaml
        private async void ExecuteYourCommand(object parameter)
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();

            try
            {
                var cmdParam = parameter as TooltipCommandParameter;
                if (cmdParam != null && ToolWindowRegistry.CategoryToIdMap.TryGetValue(cmdParam.Category, out int toolWindowId))
                {
                    await _showDocumentationHandler.HandleAsync(
                        new ShowDocumentationParams(
                            cmdParam.Path,
                            cmdParam.Category,
                            cmdParam.FunctionName,
                            cmdParam.Range
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
