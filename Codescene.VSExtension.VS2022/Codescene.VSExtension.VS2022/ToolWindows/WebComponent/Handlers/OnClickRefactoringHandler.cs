using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.CodeReviewer;
using Codescene.VSExtension.Core.Application.Services.WebComponent;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using System;
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

    public async Task HandleAsync(string path, FnToRefactorModel refactorableFunction, string entryPoint)
    {
        _path = path;

        if (AceToolWindow.IsCreated())
        {
            SetViewToLoadingMode(path);
        }

        await AceToolWindow.ShowAsync();

        // Run on background thread:
        Task.Run(() => DoRefactorAndUpdateViewAsync(path, refactorableFunction, entryPoint)).FireAndForget();
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

    private async Task DoRefactorAndUpdateViewAsync(string path, FnToRefactorModel refactorableFunction, string entryPoint)
    {
        var refactored = _aceManager.Refactor(path: path, refactorableFunction: refactorableFunction, entryPoint);

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
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
