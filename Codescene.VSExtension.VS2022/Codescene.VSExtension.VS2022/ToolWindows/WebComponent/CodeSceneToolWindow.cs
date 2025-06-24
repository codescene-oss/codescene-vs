using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static Codescene.VSExtension.Core.Application.Services.Util.Constants;
using static Codescene.VSExtension.Core.Models.WebComponent.WebComponentConstants;

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
                Devmode = true // TODO: delete
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
        var logger = await VS.GetMefServiceAsync<ILogger>();

        var message = new WebComponentMessage<CodeHealthMonitorComponentData>
        {
            MessageType = MessageTypes.UPDATE_RENDERER,
            Payload = new WebComponentPayload<CodeHealthMonitorComponentData>
            {
                IdeType = VISUAL_STUDIO_IDE_TYPE,
                View = ViewTypes.HOME,
                Data = mapper.Map(deltaCache.GetAll()),
                Devmode = true, // TODO: delete,
            }
        };
        _userControl.UpdateViewAsync(message).FireAndForget();
    }

    public override string GetTitle(int toolWindowId) => Titles.CODESCENE;

    [Guid("A9FF6E0A-51FE-4713-8123-6B75EFC3E2C5")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }
}
