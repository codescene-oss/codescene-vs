using Codescene.VSExtension.Core.Application.Services.AceManager;
using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Application.Services.WebComponent;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static Codescene.VSExtension.Core.Application.Services.Util.Constants;
using static Codescene.VSExtension.Core.Models.WebComponent.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
public class AceToolWindow : BaseToolWindow<AceToolWindow>
{
    public string FilePath { get; set; }
    public override Type PaneType => typeof(Pane);
    private static WebComponentUserControl _ctrl = null;

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();
        var mapper = await VS.GetMefServiceAsync<AceComponentMapper>();
        var handler = await VS.GetMefServiceAsync<OnClickRefactoringHandler>();

        var payload = new WebComponentPayload<AceComponentData>
        {
            IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
            View = WebComponentConstants.ViewTypes.ACE,
            Data = mapper.Map(handler.GetPath())
        };

        var ctrl = new WebComponentUserControl(payload, logger)
        {
            CloseRequested = async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await HideAsync();
            }
        };

        _ctrl = ctrl;

        return ctrl;
    }

    public override string GetTitle(int toolWindowId) => Titles.CODESCENE_ACE;

    [Guid("60f71481-a161-4512-bb43-162b852a86d1")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }

    public static void UpdateView(WebComponentMessage<AceComponentData> message)
    {
        _ctrl.UpdateViewAsync(message).FireAndForget();
        if (message.Payload?.Data?.AceResultData != null) // can be null when loading
            SendTelemetry(responseModel: message.Payload.Data.AceResultData);
    }

    public static bool IsCreated() => _ctrl != null;

    public async static Task UpdateViewAsync()
    {
        if (_ctrl == null)
        {
            await ShowAsync();
            return;
        }

        var mapper = await VS.GetMefServiceAsync<AceComponentMapper>();

        if (AceManager.LastRefactoring != null)
        {
            AceToolWindow.UpdateView(new WebComponentMessage<AceComponentData>
            {
                MessageType = WebComponentConstants.MessageTypes.UPDATE_RENDERER,
                Payload = new WebComponentPayload<AceComponentData>
                {
                    IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                    View = WebComponentConstants.ViewTypes.ACE,
                    Data = mapper.Map(AceManager.LastRefactoring)
                }
            });
        }
    }

    private static void SendTelemetry(RefactorResponseModel responseModel)
    {
        Task.Run(async () =>
        {
            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            var additionalData = new Dictionary<string, object>
                {
                    { "confidence", responseModel.Confidence.Level },
                    { "isCached", responseModel.Metadata.Cached }
                };

            telemetryManager.SendTelemetry(Constants.Telemetry.ACE_REFACTOR_PRESENTED, additionalData);
        }).FireAndForget();
    }
}