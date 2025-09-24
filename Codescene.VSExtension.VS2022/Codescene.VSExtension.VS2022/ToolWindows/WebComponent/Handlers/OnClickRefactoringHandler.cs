using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.WebComponent;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using Microsoft.VisualStudio.Shell;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

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

        if (AceToolWindow.IsCreated())
        {
            SetViewToLoadingMode(path, refactorableFunction);
        }

        await AceToolWindow.ShowAsync();

        // Run on background thread:
        Task.Run(() => DoRefactorAndUpdateViewAsync(path, refactorableFunction, entryPoint)).FireAndForget();
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
        var refactored = _aceManager.Refactor(path: path, refactorableFunction: refactorableFunction, entryPoint);

        if (refactored != null)
        {
            AceToolWindow.UpdateView(new WebComponentMessage<AceComponentData>
            {
                MessageType = WebComponentConstants.MessageTypes.UPDATE_RENDERER,
                Payload = new WebComponentPayload<AceComponentData>
                {
                    IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                    View = WebComponentConstants.ViewTypes.ACE,
                    Data = _mapper.Map(refactored)
                }
            });
        }
        else
        {
            AceToolWindow.CloseAsync().FireAndForget();
        }
    }
}
