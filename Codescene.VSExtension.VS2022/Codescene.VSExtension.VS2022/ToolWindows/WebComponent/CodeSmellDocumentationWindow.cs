using Codescene.VSExtension.Core.Application.Services.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static Codescene.VSExtension.VS2022.Util.LogHelper;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;

public class CodeSmellDocumentationWindow : BaseToolWindow<CodeSmellDocumentationWindow>
{
    private static WebComponentUserControl _userControl = null;
    private static ShowDocumentationModel _model;

    public override Type PaneType => typeof(Pane);

    public static void SetPendingPayload(ShowDocumentationModel model)
    {
        _model = model;
    }

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        var mapper = await VS.GetMefServiceAsync<CodeSmellDocumentationMapper>();

        if (_model != null)
        {
            LogAsync($"Opening doc '{_model.Category}' for file {_model.Path}", LogLevel.Info).FireAndForget();

            var payload = new WebComponentPayload<CodeSmellDocumentationComponentData>
            {
                IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                View = WebComponentConstants.ViewTypes.DOCS,
                Data = mapper.Map(_model)
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

        LogAsync($"Could not open doc '{_model.Category}' for file {_model.Path}", LogLevel.Warn).FireAndForget();
        return null;
    }

    public override string GetTitle(int toolWindowId) => "Code smell documentation";

    public static bool IsCreated() => _userControl != null;

    public static void UpdateView(WebComponentMessage<CodeSmellDocumentationComponentData> message)
    {
        _userControl.UpdateViewAsync(message).FireAndForget();
    }

    [Guid("D9D9979D-0D9C-439A-9062-33945D63FAF8")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }
}

