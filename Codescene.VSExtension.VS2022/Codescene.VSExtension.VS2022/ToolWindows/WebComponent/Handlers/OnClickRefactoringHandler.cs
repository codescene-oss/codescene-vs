using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.WebComponent;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using System.ComponentModel.Composition;
using System.IO;
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

    private string _path = null;

    public async Task HandleAsync(string path, FnToRefactorModel refactorableFunction)
    {
        _path = path;

        if (AceToolWindow.IsCreated())
        {
            SetViewToLoadingMode(path);
        }

        await AceToolWindow.ShowAsync();


        await DoRefactorAndUpdateViewAsync(path, refactorableFunction);
    }

    public string GetPath()
    {
        return _path;
    }

    private void SetViewToLoadingMode(string path)
    {
        AceToolWindow.UpdateView(new WebComponentMessage<AceComponentData>
        {
            MessageType = WebComponentConstants.MessageTypes.UPDATE_RENDERER,
            Payload = new WebComponentPayload<AceComponentData>
            {
                IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                View = WebComponentConstants.ViewTypes.ACE,
                Data = _mapper.Map(path)
            }
        });
    }

    private async Task DoRefactorAndUpdateViewAsync(string path, FnToRefactorModel refactorableFunction)
    {
        using (var reader = File.OpenText(path))
        {
            var content = await reader.ReadToEndAsync();
            var refactored = await _aceManager.Refactor(path: path, refactorableFunction: refactorableFunction);
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
    }
}
