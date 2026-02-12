// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
using static Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

public class AceToolWindow : BaseToolWindow<AceToolWindow>
{
    private static WebComponentUserControl _ctrl;
    private static int _isStale; // 0 = not stale, 1 = stale (int for Interlocked.CompareExchange)

    /// <summary>
    /// Gets a value indicating whether it gets whether the current ACE refactoring is stale (function has been modified).
    /// </summary>
    public static bool IsStale => _isStale == 1;

    public string FilePath { get; set; }

    public override Type PaneType => typeof(Pane);

    public static bool IsCreated() => _ctrl != null;

    public static async Task UpdateViewAsync(WebComponentMessage<AceComponentData> message)
    {
        // Reset stale state when a new refactoring is being displayed (loading or result)
        // This ensures the stale flag from a previous refactoring doesn't block new stale checks
        if (message.Payload?.Data?.IsStale != true)
        {
            ResetStaleState();
        }

        await _ctrl.UpdateViewAsync(message);

        // can be null when loading
        if (message.Payload?.Data?.AceResultData != null)
        {
            SendTelemetry(responseModel: message.Payload.Data.AceResultData);
        }
    }

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
        {
            return;
        }

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
            MessageType = WebComponentConstants.MessageTypes.UPDATERENDERER,
            Payload = WebComponentPayload<AceComponentData>.Create(
                WebComponentConstants.ViewTypes.ACE,
                data),
        }).FireAndForget();
    }

    /// <summary>
    /// Resets the stale state. Should be called when a new refactoring is displayed.
    /// </summary>
    public static void ResetStaleState()
    {
        Interlocked.Exchange(ref _isStale, 0);
    }

    public static async Task CloseAsync()
    {
        if (_ctrl is not null && _ctrl.CloseRequested is not null)
        {
            await _ctrl.CloseRequested();
        }
    }

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        ResetStaleState();

        var logger = await VS.GetMefServiceAsync<ILogger>();
        var mapper = await VS.GetMefServiceAsync<AceComponentMapper>();
        var handler = await VS.GetMefServiceAsync<OnClickRefactoringHandler>();

        var payload = WebComponentPayload<AceComponentData>.Create(
            WebComponentConstants.ViewTypes.ACE,
            mapper.Map(handler.Path, handler.RefactorableFunction));

        var ctrl = new WebComponentUserControl(payload, logger)
        {
            CloseRequested = async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                await HideAsync();
            },
        };

        _ctrl = ctrl;

        return ctrl;
    }

    public override string GetTitle(int toolWindowId) => Titles.CODESCENEACE;

    private static void SendTelemetry(RefactorResponseModel responseModel)
    {
        Task.Run(async () =>
        {
            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            var additionalData = new Dictionary<string, object>
                {
                    { "confidence", responseModel.Confidence.Level },
                    { "isCached", responseModel.Metadata.Cached },
                };

            telemetryManager.SendTelemetry(Telemetry.ACEREFACTORPRESENTED, additionalData);
        }).FireAndForget();
    }

    [Guid("60f71481-a161-4512-bb43-162b852a86d1")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }
}
