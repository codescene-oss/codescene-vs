using Codescene.VSExtension.Core.Application.Mappers;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.WebComponent.Data;
using Codescene.VSExtension.Core.Models.WebComponent.Message;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.Core.Models.WebComponent.Payload;
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

public class CodeSmellDocumentationWindow : BaseToolWindow<CodeSmellDocumentationWindow>
{
    private static WebComponentUserControl _userControl = null;
    private static ShowDocumentationModel _model;
    private static FnToRefactorModel _fnToRefactor;

    public override Type PaneType => typeof(Pane);

    public static void SetPendingPayload(ShowDocumentationModel model, FnToRefactorModel fnToRefactor)
    {
        _model = model;
        _fnToRefactor = fnToRefactor;
    }

    public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();
        var mapper = await VS.GetMefServiceAsync<CodeSmellDocumentationMapper>();

        var acknowledgementStateService = await VS.GetMefServiceAsync<AceAcknowledgementStateService>();
        var aceAcknowledged = acknowledgementStateService.IsAcknowledged();

        if (_model != null)
        {
            logger.Info($"Opening doc '{_model.Category}' for file {_model.Path}");

            var payload = new WebComponentPayload<CodeSmellDocumentationComponentData>
            {
                IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                View = WebComponentConstants.ViewTypes.DOCS,
                Data = mapper.Map(_model, _fnToRefactor, aceAcknowledged)
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

        logger.Warn($"Could not open doc '{_model.Category}' for file {_model.Path}");
        return null;
    }

    public override string GetTitle(int toolWindowId) => "Code smell documentation";

    public static bool IsCreated() => _userControl != null;

    public static void UpdateView(WebComponentMessage<CodeSmellDocumentationComponentData> message)
    {
        _userControl.UpdateViewAsync(message).FireAndForget();
    }

    public static async Task RefreshViewAsync()
    {
        var logger = await VS.GetMefServiceAsync<ILogger>();

        if (_userControl == null || _model == null)
        {
            logger.Warn("Could not refresh documentation tool window. Data is undefined.");
            return;
        }

        var mapper = await VS.GetMefServiceAsync<CodeSmellDocumentationMapper>();

        var acknowledgementStateService = await VS.GetMefServiceAsync<AceAcknowledgementStateService>();
        var aceAcknowledged = acknowledgementStateService.IsAcknowledged();

        try
        {
            UpdateView(new WebComponentMessage<CodeSmellDocumentationComponentData>
            {
                MessageType = WebComponentConstants.MessageTypes.UPDATE_RENDERER,
                Payload = new WebComponentPayload<CodeSmellDocumentationComponentData>
                {
                    IdeType = WebComponentConstants.VISUAL_STUDIO_IDE_TYPE,
                    View = WebComponentConstants.ViewTypes.DOCS,
                    Data = mapper.Map(_model, _fnToRefactor, aceAcknowledged),
                }
            });
        }
        catch (Exception e)
        {
            logger.Error($"Could not refresh view for {_model.Category}", e);
        }
    }

    [Guid("D9D9979D-0D9C-439A-9062-33945D63FAF8")]
    internal class Pane : ToolWindowPane
    {
        public Pane() => BitmapImageMoniker = KnownMonikers.StatusInformation;
    }
}

