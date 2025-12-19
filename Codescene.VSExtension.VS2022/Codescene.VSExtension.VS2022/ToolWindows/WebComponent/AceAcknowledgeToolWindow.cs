using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.VS2022.Application.Services;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

public class AceAcknowledgeToolWindow : BaseToolWindow<AceAcknowledgeToolWindow>
{
    public override Type PaneType => typeof(Pane);
    private static WebComponentUserControl _ctrl = null;

    private static FnToRefactorModel _fnToRefactor;
    private static string _filePath;

    public static void UpdateRefactoringData(FnToRefactorModel fnToRefactor, string path)
    {
        _fnToRefactor = fnToRefactor;
        _filePath = path;
    }

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();
        var acknowledgementStateService = await VS.GetMefServiceAsync<AceAcknowledgementStateService>();

        var payload = new WebComponentPayload<AceAcknowledgeComponentData>
        {
            IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
            View = WebComponentConstants.ViewTypes.ACE_ACKNOWLEDGE,
            Data = new AceAcknowledgeComponentData
            {
                FilePath = _filePath,
                AutoRefactor = new AutoRefactorConfig
                {
                    Activated = acknowledgementStateService.IsAcknowledged(),
                    Disabled = false, // TODO: determine based on presence of ACE token (CS-5670)
                    Visible = true
                },
                FnToRefactor = _fnToRefactor,
            }
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

    public async static Task UpdateViewAsync()
    {
        if (_ctrl == null)
        {
            await ShowAsync();
            return;
        }

        var acknowledgementStateService = await VS.GetMefServiceAsync<AceAcknowledgementStateService>();

        var message = new WebComponentMessage<AceAcknowledgeComponentData>
        {
            MessageType = WebComponentConstants.MessageTypes.UPDATE_RENDERER,
            Payload = new WebComponentPayload<AceAcknowledgeComponentData>
            {
                IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                View = WebComponentConstants.ViewTypes.ACE_ACKNOWLEDGE,
                Data = new AceAcknowledgeComponentData
                {
                    FilePath = _filePath,
                    AutoRefactor = new AutoRefactorConfig
                    {
                        Activated = acknowledgementStateService.IsAcknowledged(),
                        Disabled = false, // TODO: determine based on presence of ACE token (CS-5670)
                        Visible = true
                    },
                    FnToRefactor = _fnToRefactor,
                }
            }
        };

        await _ctrl.UpdateViewAsync(message);
        await ShowAsync();
    }

    public override string GetTitle(int toolWindowId) => "ACE Acknowledgement";

    [Guid("B5AE467B-9A17-4496-95A7-87DCE4703275")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }

    public static bool IsCreated() => _ctrl != null;

}