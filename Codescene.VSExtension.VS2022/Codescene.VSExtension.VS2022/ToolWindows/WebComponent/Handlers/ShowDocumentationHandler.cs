using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Community.VisualStudio.Toolkit;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Models.WebComponent.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

[Export(typeof(ShowDocumentationHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class ShowDocumentationHandler
{
    public async Task HandleAsync(ShowDocumentationModel model)
    {
        if (CodeSmellDocumentationWindow.IsCreated())
        {
            await SetViewToLoadingModeAsync(model);
        }

        CodeSmellDocumentationWindow.SetPendingPayload(model);

        await CodeSmellDocumentationWindow.ShowAsync();
    }

    private async Task SetViewToLoadingModeAsync(ShowDocumentationModel model)
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();
        var mapper = await VS.GetMefServiceAsync<CodeSmellDocumentationMapper>();

        try
        {
            //loading
            CodeSmellDocumentationWindow.UpdateView(new WebComponentMessage<CodeSmellDocumentationComponentData>
            {
                MessageType = MessageTypes.UPDATE_RENDERER,
                Payload = new WebComponentPayload<CodeSmellDocumentationComponentData>
                {
                    IdeType = VISUAL_STUDIO_IDE_TYPE,
                    View = ViewTypes.DOCS,
                    Data = mapper.Map(model),
                    Devmode = true,
                }
            });
        }
        catch (Exception e)
        {
            logger.Error($"Could not update view for {model.Category}", e);
        }
    }

}
