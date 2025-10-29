using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.VS2022.Commands;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Windows.Input;

namespace Codescene.VSExtension.VS2022.UnderlineTagger
{
    [Export(typeof(AceRefactorTooltipModel))]
    [PartCreationPolicy(CreationPolicy.NonShared)]
    public class AceRefactorTooltipModel
    {
        public string Path { get; set; }

        public FnToRefactorModel RefactorableFunction { get; set; }

        public ICommand RefactorCommand { get; }

        private readonly OnClickRefactoringHandler _onClickRefactoringHandler;

        [ImportingConstructor]
        public AceRefactorTooltipModel(OnClickRefactoringHandler onClickRefactoringHandler)
        {
            _onClickRefactoringHandler = onClickRefactoringHandler;
            RefactorCommand = new RelayCommand(ExecuteRefactorCommand);
        }

        //Bindings are defined in UnderlineTaggerTooltip.xaml
        private async void ExecuteRefactorCommand(object parameter)
        {
            var logger = await VS.GetMefServiceAsync<ILogger>();

            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await _onClickRefactoringHandler.HandleAsync(
                    this._onClickRefactoringHandler.Path, 
                    this._onClickRefactoringHandler.RefactorableFunction, 
                    AceConstants.AceEntryPoint.INTENTION_ACTION);
            }
            catch (Exception e)
            {
                logger.Error("Unable to handle tagger action.", e);
            }
        }
    }
}