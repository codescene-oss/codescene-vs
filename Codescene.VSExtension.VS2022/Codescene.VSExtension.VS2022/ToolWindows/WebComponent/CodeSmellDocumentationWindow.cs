using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

public class CodeSmellDocumentationWindow : BaseToolWindow<CodeSmellDocumentationWindow>
{
    private static WebComponentUserControl _userControl = null;
    private static ShowDocumentationModel _model;

    public override Type PaneType => typeof(Pane);

    public static async void SetPendingPayload(ShowDocumentationModel model)
    {
        _model = model;
    }

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();
        var mapper = await VS.GetMefServiceAsync<CodeSmellDocumentationMapper>();

        if (_model != null)
        {
            var payload = new WebComponentPayload<CodeSmellDocumentationComponentData>
            {
                IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                View = WebComponentConstants.ViewTypes.DOCS,
                Devmode = true,
                Data = mapper.Map(_model)
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

        return null;
    }

    public override string GetTitle(int toolWindowId) => "Code smell documentation";

    public static bool IsCreated() => _userControl != null;

    public static void UpdateView(WebComponentMessage<CodeSmellDocumentationComponentData> message)
    {
        _userControl.UpdateView(message);
    }

    [Guid("D9D9979D-0D9C-439A-9062-33945D63FAF8")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }
}

