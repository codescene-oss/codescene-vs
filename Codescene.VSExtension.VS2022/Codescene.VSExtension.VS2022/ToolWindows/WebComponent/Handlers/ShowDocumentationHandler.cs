using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Message;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.Core.Models.WebComponent.Payload;
using Codescene.VSExtension.VS2022.Application.Services;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using static Codescene.VSExtension.Core.Consts.WebComponentConstants;

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
    public async Task HandleAsync(ShowDocumentationModel model, FnToRefactorModel fnToRefactor, string entryPoint = DocsEntryPoint.DiagnosticItem)
    {
        SendTelemetry(entryPoint, model.Category);

        if (CodeSmellDocumentationWindow.IsCreated())
        {
            await SetViewToLoadingModeAsync(model, fnToRefactor);
        }

        CodeSmellDocumentationWindow.SetPendingPayload(model, fnToRefactor);

        await CodeSmellDocumentationWindow.ShowAsync();
    }

    private async Task SetViewToLoadingModeAsync(ShowDocumentationModel model, FnToRefactorModel fnToRefactor)
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();
        var mapper = await VS.GetMefServiceAsync<CodeSmellDocumentationMapper>();

        var acknowledgementStateService = await VS.GetMefServiceAsync<AceAcknowledgementStateService>();
        var aceAcknowledged = acknowledgementStateService.IsAcknowledged();

        try
        {
            logger.Info($"Opening doc '{model.Category}' for file {model.Path}");

            CodeSmellDocumentationWindow.UpdateView(new WebComponentMessage<CodeSmellDocumentationComponentData>
            {
                MessageType = MessageTypes.UPDATERENDERER,
                Payload = new WebComponentPayload<CodeSmellDocumentationComponentData>
                {
                    IdeType = VISUALSTUDIOIDETYPE,
                    View = ViewTypes.DOCS,
                    Data = mapper.Map(model, fnToRefactor, aceAcknowledged),
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
            telemetryManager.SendTelemetry(Constants.Telemetry.OPENDOCSPANEL, additionalData);
        }).FireAndForget();
    }
}
