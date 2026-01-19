using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.PreflightManager;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.VS2022.CodeLens;
using Codescene.VSExtension.VS2022.Commands;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Text;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
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

        public CodeSmellTooltipModel CommandParameter => new()
        {
            Category = Category,
            Details = Details,
            Path = Path,
            FunctionName = FunctionName,
            Range = new CodeSmellRangeModel(
                Range.StartLine,
                Range.EndLine,
                Range.StartColumn,
                Range.EndColumn
            ),
            FunctionRange = FunctionRange is null
            ? null
            : new CodeSmellRangeModel(
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
                    var fnToRefactor = await GetRefactorableFunctionAsync(cmdParam);

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

        private async Task<FnToRefactorModel> GetRefactorableFunctionAsync(CodeSmellTooltipModel cmdParam)
        {
            var preflightManager = await VS.GetMefServiceAsync<IPreflightManager>();
            var aceManager = await VS.GetMefServiceAsync<IAceManager>();

            var preflight = preflightManager.GetPreflightResponse();

            if (cmdParam.FunctionRange == null) return null;

            var codeSmell = new CliCodeSmellModel()
            {
                Details = cmdParam.Details,
                Category = cmdParam.Category,
                Range = new Core.Models.Cli.CliRangeModel()
                {
                    StartColumn = cmdParam.FunctionRange.StartColumn,
                    EndColumn = cmdParam.FunctionRange.EndColumn,
                    Startline = cmdParam.FunctionRange.StartLine,
                    EndLine = cmdParam.FunctionRange.EndLine,
                },
            };

            // Get the current code snapshot from the document
            string fileContent = "";
            var docView = await VS.Documents.OpenAsync(cmdParam.Path);
            if (docView?.TextBuffer is ITextBuffer buffer)
            {
                fileContent = buffer.CurrentSnapshot.GetText();
            }

            var refactorableFunctions = aceManager.GetRefactorableFunctions(cmdParam.Path, fileContent, [codeSmell], preflight);
            return refactorableFunctions?.FirstOrDefault();
        }
    }
}
