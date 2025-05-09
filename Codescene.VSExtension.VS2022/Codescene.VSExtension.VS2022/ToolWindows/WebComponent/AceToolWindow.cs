using Codescene.VSExtension.Core.Application.Services.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
public class AceToolWindow : BaseToolWindow<AceToolWindow>
{
    public string FilePath { get; set; }
    public override Type PaneType => typeof(Pane);
    private static WebComponentUserControl _ctrl = null;

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        var mapper = await VS.GetMefServiceAsync<WebComponentMapper>();

        var handler = await VS.GetMefServiceAsync<OnClickRefactoringHandler>();

        var payload = new WebComponentPayload
        {
            IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
            View = WebComponentConstants.ViewTypes.ACE,
            Data = mapper.Map(handler.GetPath())
        };

        var ctrl = new WebComponentUserControl(payload)
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

    public override string GetTitle(int toolWindowId) => "Refactoring suggestion";

    [Guid("60f71481-a161-4512-bb43-162b852a86d1")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }

    public static void UpdateView(WebComponentMessage message)
    {
        _ctrl.UpdateView(message);
    }

    public static bool IsCreated() => _ctrl != null;
}