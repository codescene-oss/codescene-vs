using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Application.Services.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Models.WebComponent.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

public static class DocsEntryPoint
{
    public const string DiagnosticItem = "diagnostic-item";
    public const string CodeHealthMonitor = "code-health-monitor";
}

[Export(typeof(ShowDocumentationHandler))]
[PartCreationPolicy(CreationPolicy.Shared)]
public class ShowDocumentationHandler
{
    public async Task HandleAsync(ShowDocumentationModel model, string entryPoint = DocsEntryPoint.DiagnosticItem)
    {
        SendTelemetry(entryPoint, model.Category);

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
            logger.Info($"Opening doc '{model.Category}' for file {model.Path}");

            CodeSmellDocumentationWindow.UpdateView(new WebComponentMessage<CodeSmellDocumentationComponentData>
            {
                MessageType = MessageTypes.UPDATE_RENDERER,
                Payload = new WebComponentPayload<CodeSmellDocumentationComponentData>
                {
                    IdeType = VISUAL_STUDIO_IDE_TYPE,
                    View = ViewTypes.DOCS,
                    Data = mapper.Map(model),
                }
            });
        }
        catch (Exception e)
        {
            logger.Warn($"Could not open doc '{model.Category}' for file {model.Path}");
            logger.Error($"Could not update view for {model.Category}", e);
        }
    }

    private void SendTelemetry(string entryPoint, string category)
    {
        Task.Run(async () =>
        {
            var additionalData = new Dictionary<string, object>
            {
                { "source", entryPoint },
                { "category", category }
            };

            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            telemetryManager.SendTelemetry(Constants.Telemetry.OPEN_DOCS_PANEL, additionalData);
        }).FireAndForget();
    }
}
