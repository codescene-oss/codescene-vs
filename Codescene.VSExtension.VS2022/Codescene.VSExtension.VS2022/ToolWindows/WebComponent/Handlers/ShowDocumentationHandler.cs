using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Application.Services.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Models.WebComponent.WebComponentConstants;
using static Codescene.VSExtension.VS2022.Util.LogHelper;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

public static class DocsEntryPoint
{
    public const string DiagnosticItem = "diagnostic-item";
    public const string CodeHealthMonitor = "code-health-monitor"; // Only on Premium
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
        var mapper = await VS.GetMefServiceAsync<CodeSmellDocumentationMapper>();

        try
        {
            LogAsync($"Opening doc '{model.Category}' for file {model.Path}", LogLevel.Info).FireAndForget();

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
            LogAsync($"Could not open doc '{model.Category}' for file {model.Path}", LogLevel.Warn).FireAndForget();
            LogAsync($"Could not update view for {model.Category}", LogLevel.Error, e).FireAndForget();
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
