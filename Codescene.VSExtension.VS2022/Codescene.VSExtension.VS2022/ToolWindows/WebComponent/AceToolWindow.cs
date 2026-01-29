using Codescene.VSExtension.Core.Application.Ace;
using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Message;
using Codescene.VSExtension.Core.Models.WebComponent.Payload;
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
using static Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
public class AceToolWindow : BaseToolWindow<AceToolWindow>
{
    public string FilePath { get; set; }
    public override Type PaneType => typeof(Pane);
    private static WebComponentUserControl _ctrl = null;
    private static int _isStale = 0; // 0 = not stale, 1 = stale (int for Interlocked.CompareExchange)

    /// <summary>
    /// Gets whether the current ACE refactoring is stale (function has been modified).
    /// </summary>
    public static bool IsStale => _isStale == 1;

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        ResetStaleState();

        var logger = await VS.GetMefServiceAsync<ILogger>();
        var mapper = await VS.GetMefServiceAsync<AceComponentMapper>();
        var handler = await VS.GetMefServiceAsync<OnClickRefactoringHandler>();

        var payload = new WebComponentPayload<AceComponentData>
        {
            IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
            View = WebComponentConstants.ViewTypes.ACE,
            Data = mapper.Map(handler.Path, handler.RefactorableFunction)
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
        // Reset stale state when a new refactoring is being displayed (loading or result)
        // This ensures the stale flag from a previous refactoring doesn't block new stale checks
        if (message.Payload?.Data?.IsStale != true)
            ResetStaleState();

        _ctrl.UpdateViewAsync(message).FireAndForget();
        if (message.Payload?.Data?.AceResultData != null) // can be null when loading
            SendTelemetry(responseModel: message.Payload.Data.AceResultData);
    }

    public static bool IsCreated() => _ctrl != null;

    /// <summary>
    /// Marks the current ACE refactoring as stale and updates the view.
    /// Called when the function being refactored has been modified.
    /// Uses Interlocked.CompareExchange for atomic check-and-set to prevent race conditions.
    /// </summary>
    public static async Task MarkAsStaleAsync()
    {
        // Atomically attempt to swap _isStale from 0 (not stale) to 1 (stale).
        // Only proceeds if we successfully made the transition, preventing duplicate updates.
        if (Interlocked.CompareExchange(ref _isStale, 1, 0) != 0)
            return;

        // Validate required dependencies before proceeding
        if (_ctrl == null || AceManager.LastRefactoring == null)
        {
            // Reset state since we can't complete the operation
            Interlocked.Exchange(ref _isStale, 0);
            return;
        }

        var mapper = await VS.GetMefServiceAsync<AceComponentMapper>();
        var data = mapper.MapAsStale(AceManager.LastRefactoring);

        _ctrl.UpdateViewAsync(new WebComponentMessage<AceComponentData>
        {
            MessageType = WebComponentConstants.MessageTypes.UPDATE_RENDERER,
            Payload = new WebComponentPayload<AceComponentData>
            {
                IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                View = WebComponentConstants.ViewTypes.ACE,
                Data = data
            }
        }).FireAndForget();
    }

    /// <summary>
    /// Resets the stale state. Should be called when a new refactoring is displayed.
    /// </summary>
    public static void ResetStaleState()
    {
        Interlocked.Exchange(ref _isStale, 0);
    }

    public async static Task UpdateViewAsync()
    {
        if (_ctrl == null)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            await ShowAsync();
            return;
        }

        ResetStaleState();

        var mapper = await VS.GetMefServiceAsync<AceComponentMapper>();

        if (AceManager.LastRefactoring != null)
        {
            UpdateView(new WebComponentMessage<AceComponentData>
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

    public static async Task CloseAsync()
    {
        if (_ctrl.CloseRequested is not null)
            await _ctrl.CloseRequested();
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