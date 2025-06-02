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
using static Codescene.VSExtension.Core.Models.WebComponent.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

public class CodeSmellDocumentationWindow : BaseToolWindow<CodeSmellDocumentationWindow>
{
    private static WebComponentUserControl _userControl = null;

    public override Type PaneType => typeof(Pane);

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        var mapper = await VS.GetMefServiceAsync<WebComponentMapper>();

        var payload = new WebComponentPayload
        {
            IdeType = VISUAL_STUDIO_IDE_TYPE,
            View = ViewTypes.DOCS,
            Data = mapper.Map("")
        };

        var ctrl = new WebComponentUserControl(payload)
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

    public override string GetTitle(int toolWindowId) => "CodeScene - Code smell documentation";

    public static bool IsCreated() => _userControl != null;

    public static void UpdateView(ShowDocsMessage message)
    {
        _userControl.UpdateView(message);
    }

    [Guid("D9D9979D-0D9C-439A-9062-33945D63FAF8")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }
}

