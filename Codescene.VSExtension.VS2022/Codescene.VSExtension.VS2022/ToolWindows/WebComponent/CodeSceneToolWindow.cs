using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.VS2022.Util;
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
using static Codescene.VSExtension.VS2022.Util.LogHelper;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

public class CodeSceneToolWindow : BaseToolWindow<CodeSceneToolWindow>
{
    private static WebComponentUserControl _userControl = null;

    public override Type PaneType => typeof(Pane);

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new WebComponentPayload<CodeHealthMonitorComponentData>
            {
                IdeType = VISUAL_STUDIO_IDE_TYPE,
                View = ViewTypes.HOME,
                Data = new CodeHealthMonitorComponentData
                {
                    ShowOnboarding = false,
                    FileDeltaData = [],
                    Jobs = []
                }
            };

            var ctrl = new WebComponentUserControl(payload)
            {
                CloseRequested = async () =>
                {
                    await HideAsync();
                }
            };

            _userControl = ctrl;

            return ctrl;
        }
        catch (Exception ex)
        {
            LogAsync("Could not create tool window.", LogLevel.Error, ex).FireAndForget();
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

        var message = new WebComponentMessage<CodeHealthMonitorComponentData>
        {
            MessageType = MessageTypes.UPDATE_RENDERER,
            Payload = new WebComponentPayload<CodeHealthMonitorComponentData>
            {
                IdeType = VISUAL_STUDIO_IDE_TYPE,
                View = ViewTypes.HOME,
                Data = new CodeHealthMonitorComponentData
                {
                    ShowOnboarding = false,
                    FileDeltaData = [],
                    Jobs = []
                },
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
