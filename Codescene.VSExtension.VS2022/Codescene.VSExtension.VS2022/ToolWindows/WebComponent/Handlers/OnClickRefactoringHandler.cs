using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.WebComponent;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.VS2022.Application.Services;
using Community.VisualStudio.Toolkit;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Models.WebComponent.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

[Export(typeof(OnClickRefactoringHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class OnClickRefactoringHandler
{
    [Import]
    private readonly AceComponentMapper _mapper;

    [Import]
    private readonly IAceManager _aceManager;

    public string Path { get; private set; }

    public FnToRefactorModel RefactorableFunction { get; private set; }

    public async Task HandleAsync(string path, FnToRefactorModel refactorableFunction, string entryPoint)
    {
        Path = path;
        RefactorableFunction = refactorableFunction;

        // Check if user has acknowledged ACE usage
        var acknowledgementStateService = await VS.GetMefServiceAsync<AceAcknowledgementStateService>();
        if (!acknowledgementStateService.IsAcknowledged())
        {
            AceAcknowledgeToolWindow.UpdateRefactoringData(refactorableFunction, path);

            if (AceAcknowledgeToolWindow.IsCreated())
                await AceAcknowledgeToolWindow.UpdateViewAsync();
            else
                await AceAcknowledgeToolWindow.ShowAsync();

            return;
        }

        if (AceToolWindow.IsCreated())
        {
            SetViewToLoadingMode(path, refactorableFunction);
        }

        await AceToolWindow.ShowAsync();

        // Run on background thread:
        Task.Run(() => DoRefactorAndUpdateViewAsync(Path, RefactorableFunction, entryPoint)).FireAndForget();
    }

    private void SetViewToLoadingMode(string path, FnToRefactorModel refactorableFunction)
    {
        AceToolWindow.UpdateView(new WebComponentMessage<AceComponentData>
        {
            MessageType = WebComponentConstants.MessageTypes.UPDATE_RENDERER,
            Payload = new WebComponentPayload<AceComponentData>
            {
                IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                View = WebComponentConstants.ViewTypes.ACE,
                Data = _mapper.Map(path, refactorableFunction)
            }
        });
    }

    private async Task DoRefactorAndUpdateViewAsync(string path, FnToRefactorModel refactorableFunction, string entryPoint)
    {
        try
        {
            var refactored = _aceManager.Refactor(path: path, refactorableFunction: refactorableFunction, entryPoint);
            AceComponentData data;
            if (refactored != null)
                data = _mapper.Map(refactored);
            else
                data = _mapper.Map(path, refactorableFunction, AceViewErrorTypes.GENERIC);

            AceToolWindow.UpdateView(new WebComponentMessage<AceComponentData>
            {
                MessageType = WebComponentConstants.MessageTypes.UPDATE_RENDERER,
                Payload = new WebComponentPayload<AceComponentData>
                {
                    IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                    View = WebComponentConstants.ViewTypes.ACE,
                    Data = data
                }
            });
        }
        catch (Exception ex)
        {
            // Determine error type based on exception
            string errorType = DetermineErrorType(ex);

            AceToolWindow.UpdateView(new WebComponentMessage<AceComponentData>
            {
                MessageType = WebComponentConstants.MessageTypes.UPDATE_RENDERER,
                Payload = new WebComponentPayload<AceComponentData>
                {
                    IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                    View = WebComponentConstants.ViewTypes.ACE,
                    Data = _mapper.Map(path, refactorableFunction, errorType)
                }
            });
        }
    }

    private string DetermineErrorType(Exception ex)
    {
        if (ex != null)
        {
            var isMissingToken = ex is MissingAuthTokenException;
            var isAuthError = ex is DevtoolsException exception && exception.Status == 401;
            if (isMissingToken || isAuthError)
            {
                return AceViewErrorTypes.AUTH;
            }
        }

        return AceViewErrorTypes.GENERIC;
    }
}
