using Codescene.VSExtension.VS2022.CodeLens;
using Codescene.VSExtension.VS2022.Commands;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
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

        public ICommand YourCommand { get; }
        public int WindowId { get; set; }

        private readonly ShowDocumentationHandler _showDocumentationHandler;

        [ImportingConstructor]
        public UnderlineTaggerTooltipModel(ShowDocumentationHandler showDocumentationHandler)
        {
            _showDocumentationHandler = showDocumentationHandler;
            YourCommand = new RelayCommand(ExecuteYourCommand);
        }

        private async void ExecuteYourCommand(object parameter)
        {
            var category = parameter as string;
            if (ToolWindowRegistry.CategoryToIdMap.TryGetValue(category, out int toolWindowId))
            {
                await _showDocumentationHandler.HandleAsync("somePath");
            }
        }
    }
}
