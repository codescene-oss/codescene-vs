using Codescene.VSExtension.Core.Application.Cache.Review;
using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Interfaces.Util;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Message;
using Codescene.VSExtension.Core.Models.WebComponent.Payload;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static Codescene.VSExtension.Core.Consts.Constants;
using static Codescene.VSExtension.Core.Consts.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

public class CodeSceneToolWindow : BaseToolWindow<CodeSceneToolWindow>
{
    private static WebComponentUserControl _userControl = null;

    public override Type PaneType => typeof(Pane);

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();

        try
        {
            var deltaCache = new DeltaCacheService();
            var mapper = await VS.GetMefServiceAsync<CodeHealthMonitorMapper>();

            var payload = new WebComponentPayload<CodeHealthMonitorComponentData>
            {
                IdeType = VISUAL_STUDIO_IDE_TYPE,
                View = ViewTypes.HOME,
                Data = mapper.Map(deltaCache.GetAll()),
                Pro = true
            };

            var ctrl = new WebComponentUserControl(payload, logger)
            {
                CloseRequested = async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    await HideAsync();
                }
            };

            _userControl = ctrl;

            return ctrl;
        }
        catch (Exception ex)
        {
            logger.Error("Could not create tool window.", ex);
            return null;
        }
    }

    public async static Task UpdateViewAsync()
    {
        if (_userControl == null)
        {
            await ShowAsync();
            return;
        }

        var deltaCache = new DeltaCacheService();
        var mapper = await VS.GetMefServiceAsync<CodeHealthMonitorMapper>();

        var message = new WebComponentMessage<CodeHealthMonitorComponentData>
        {
            MessageType = MessageTypes.UPDATE_RENDERER,
            Payload = new WebComponentPayload<CodeHealthMonitorComponentData>
            {
                IdeType = VISUAL_STUDIO_IDE_TYPE,
                View = ViewTypes.HOME,
                Data = mapper.Map(deltaCache.GetAll()),
                Pro = true
            }
        };
        ILogger logger = await VS.GetMefServiceAsync<ILogger>();
        _userControl.UpdateViewAsync(message).FireAndForget();
    }

    public override string GetTitle(int toolWindowId) => Titles.CODESCENE;

    [Guid("A9FF6E0A-51FE-4713-8123-6B75EFC3E2C5")]
    internal class Pane : ToolWindowPane, IVsWindowFrameNotify3
    {
        private IDebounceService _debounceService;

        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;

        public int OnClose(ref uint pgrfSaveOptions)
        {
            SendTelemetry(false);
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Called when the tool window is shown or its visibility changes.
        /// See: https://learn.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.__frameshow?view=visualstudiosdk-2022
        /// </summary>
        /// <remarks>
        /// This event can be triggered multiple times when the window is shown due to internal Visual Studio window management behaviors.
        /// To avoid flooding telemetry and excessive processing, the invocation of telemetry reporting is debounced with a delay of 5 seconds.
        /// </remarks>
        public int OnShow(int fShow)
        {
            // FRAMESHOW_WinShown (1), FRAMESHOW_TabActivated (2)
            var allowedCodes = new List<int>() { 1, 2 };
            if (!allowedCodes.Contains(fShow)) return VSConstants.S_OK;

            Task.Run(async () =>
            {
                _debounceService ??= await VS.GetMefServiceAsync<IDebounceService>();

                _debounceService?.Debounce(
                    nameof(CodeSceneToolWindow),
                    () => { SendTelemetry(true); },
                    TimeSpan.FromSeconds(5)
                );
            }).FireAndForget();

            return VSConstants.S_OK;
        }

        public int OnMove(int x, int y, int w, int h) => VSConstants.S_OK;

        public int OnSize(int x, int y, int w, int h) => VSConstants.S_OK;

        public int OnDockableChange(int fDockable, int x, int y, int w, int h) => VSConstants.S_OK;

        private void SendTelemetry(bool visible)
        {
            Task.Run(async () =>
            {
                var additionalData = new Dictionary<string, object>
                {
                    { "visible", visible }
                };

                var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
                telemetryManager.SendTelemetry(Telemetry.MONITOR_VISIBILITY, additionalData);
            }).FireAndForget();
        }
    }
}
