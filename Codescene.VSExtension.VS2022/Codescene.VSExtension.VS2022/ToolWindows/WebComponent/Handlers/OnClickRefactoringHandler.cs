using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.WebComponent;
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
    private readonly WebComponentMapper _mapper;

    [Import]
    private readonly ICodeReviewer _reviewer;

    private string _path = null;

    public async Task HandleAsync(string path)
    {
        _path = path;

        if (AceToolWindow.IsCreated())
        {
            SetViewToLoadingMode(path);
        }

        await AceToolWindow.ShowAsync();


        await DoRefactorAndUpdateViewAsync(path);
    }

    public string GetPath()
    {
        return _path;
    }

    private void SetViewToLoadingMode(string path)
    {
        AceToolWindow.UpdateView(new WebComponentMessage
        {
            MessageType = WebComponentConstants.MessageTypes.UPDATE_RENDERER,
            Payload = new WebComponentPayload
            {
                IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                View = WebComponentConstants.ViewTypes.ACE,
                Data = _mapper.Map(path)
            }
        });
    }

    private async Task DoRefactorAndUpdateViewAsync(string path)
    {
        using (var reader = File.OpenText(path))
        {
            var content = await reader.ReadToEndAsync();
            var refactored = await _reviewer.Refactor(path: path, content: content);
            AceToolWindow.UpdateView(new WebComponentMessage
            {
                MessageType = WebComponentConstants.MessageTypes.UPDATE_RENDERER,
                Payload = new WebComponentPayload
                {
                    IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                    View = WebComponentConstants.ViewTypes.ACE,
                    Data = _mapper.Map(refactored)
                }
            });
        }
    }
}
