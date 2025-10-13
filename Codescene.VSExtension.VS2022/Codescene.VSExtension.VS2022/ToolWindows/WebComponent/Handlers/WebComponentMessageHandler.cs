using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.VS2022.CommitBaseline;
using Codescene.VSExtension.VS2022.Review;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Models.WebComponent.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

internal class WebComponentMessageHandler
{
    private ILogger _logger;

    private ShowDocumentationHandler _showDocsHandler;

    private readonly WebComponentUserControl _control;
    private readonly IReviewService _reviewService;

    public WebComponentMessageHandler(WebComponentUserControl control)
    {
        _control = control;
    }

    /// <summary>
    /// Handles messages sent from the WebView component to the native Visual Studio extension.
    /// Processes different message types by dispatching to the appropriate handler services.
    /// </summary>
    public async Task HandleAsync(string message)
    {
        _logger ??= await VS.GetMefServiceAsync<ILogger>();
        _showDocsHandler ??= await VS.GetMefServiceAsync<ShowDocumentationHandler>();

        MessageObj<JToken> msgObject;
        try
        {
            msgObject = JsonConvert.DeserializeObject<MessageObj<JToken>>(message);
        }
        catch (Exception ex)
        {
            _logger.Error($"Unable to process webview message. Deserialization failed.", ex);
            return;
        }

        _logger.Debug($"Received message from webview: '{msgObject?.MessageType}'.");

        await ProcessMessageAsync(msgObject, _logger);
    }

    private async Task ProcessMessageAsync(MessageObj<JToken> msgObject, ILogger logger)
    {
        if (msgObject?.MessageType == null)
        {
            logger.Debug("Unable to process webview message: missing MessageType.");
            return;
        }

        logger.Debug($"Handling '{msgObject.MessageType}' message.");

        try
        {
            switch (msgObject.MessageType)
            {
                case MessageTypes.INIT:
                    await HandleInitAsync(msgObject, logger);
                    break;

                case MessageTypes.COPY_CODE:
                    await HandleCopyCodeAsync();
                    break;

                case MessageTypes.SHOW_DIFF:
                    await HandleShowDiffAsync();
                    break;

                case MessageTypes.APPLY:
                    await HandleApplyAsync();
                    break;

                case MessageTypes.REJECT:
                    await HandleRejectAsync();
                    break;

                case MessageTypes.GOTO_FUNCTION_LOCATION:
                    await HandleGotoFunctionLocationAsync(msgObject, logger);
                    break;

                case MessageTypes.OPEN_DOCS_FOR_FUNCTION:
                    await HandleOpenDocsForFunctionAsync(msgObject, logger);
                    break;

                case MessageTypes.COMMIT_BASELINE:
                    await HandleCommitBaselineAsync(msgObject, logger);
                    break;

                default:
                    logger.Debug($"Unknown message type: {msgObject.MessageType}");
                    break;
            }
        }
        catch (Exception e)
        {
            _logger.Error($"Unable to handle '{msgObject.MessageType}'", e);
        }
    }

    private async Task HandleInitAsync(MessageObj<JToken> msgObject, ILogger logger)
    {
        var source = msgObject.Payload?.ToString();
        if (string.IsNullOrEmpty(source)) return;

        logger.Debug($"Webview '{source}' is ready to take messages.");

        if (source == ViewTypes.HOME)
        {
            await CodeSceneToolWindow.UpdateViewAsync().ConfigureAwait(false);
        }
    }

    private async Task HandleCopyCodeAsync()
    {
        // TODO: add additionalData to telemetry
        //var additionalData = new Dictionary<string, object>
        //{
        //    { "traceId", ... },
        //    { "skipCache ", ... }
        //};
        SendTelemetry(Constants.Telemetry.ACE_REFACTOR_COPY_CODE);

        var copyHandler = await VS.GetMefServiceAsync<CopyRefactoredCodeHandler>();
        copyHandler.CopyToRefactoredCodeToClipboard();
    }

    private async Task HandleShowDiffAsync()
    {
        // TODO: add additionalData to telemetry
        //var additionalData = new Dictionary<string, object>
        //{
        //    { "traceId", ... },
        //    { "skipCache ", ... }
        //};
        SendTelemetry(Constants.Telemetry.ACE_REFACTOR_DIFF_SHOWN);

        var diffHandler = await VS.GetMefServiceAsync<ShowDiffHandler>();
        await diffHandler.ShowDiffWindowAsync();
    }

    private async Task HandleApplyAsync()
    {
        // TODO: add additionalData to telemetry
        //var additionalData = new Dictionary<string, object>
        //{
        //    { "traceId", ... },
        //    { "skipCache ", ... }
        //};
        SendTelemetry(Constants.Telemetry.ACE_REFACTOR_APPLIED);

        var applier = await VS.GetMefServiceAsync<RefactoringChangesApplier>();
        await applier.ApplyAsync();
    }

    private async Task HandleRejectAsync()
    {
        // TODO: add additionalData to telemetry
        //var additionalData = new Dictionary<string, object>
        //{
        //    { "traceId", ... },
        //    { "skipCache ", ... }
        //};
        SendTelemetry(Constants.Telemetry.ACE_REFACTOR_REJECTED);

        if (_control.CloseRequested is not null)
            await _control.CloseRequested();
    }

    private async Task HandleGotoFunctionLocationAsync(MessageObj<JToken> msgObject, ILogger logger)
    {
        var payload = msgObject.Payload.ToObject<GotoFunctionLocationPayload>();
        var startLine = payload.Fn?.Range?.StartLine ?? 1;  // When opening files without focus on specific line, Fn is null.

        await DocumentNavigator.OpenFileAndGoToLineAsync(
            payload.FileName,
            startLine,
            logger
        );
    }

    private async Task HandleOpenDocsForFunctionAsync(MessageObj<JToken> msgObject, ILogger logger)
    {
        var payload = msgObject.Payload.ToObject<OpenDocsForFunctionPayload>();
        _logger.Debug($"Opening '{payload.DocType}'...");

        await _showDocsHandler?.HandleAsync(
        new ShowDocumentationModel(
            payload.FileName,
            payload.DocType,
            payload.Fn?.Name,
            payload.Fn?.Range), DocsEntryPoint.CodeHealthMonitor
        );
    }

    private async Task HandleCommitBaselineAsync(MessageObj<JToken> msgObject, ILogger logger)
    {
        var newBaseline = msgObject.Payload?.ToString();
        if (string.IsNullOrEmpty(newBaseline)) return;

        _logger.Debug($"Commit baseline type requested from UI: '{newBaseline}'...");

        var _commitBaselineService = await VS.GetMefServiceAsync<CommitBaselineService>();
        var currentBaseline = _commitBaselineService.GetCommitBaseline();

        if (!Enum.TryParse<CommitBaselineType>(newBaseline, true, out var baselineType))
        {
            _logger.Warn($"Unknown baseline type received: {newBaseline}");
            return;
        }

        if (currentBaseline != newBaseline)
        {
            await CodeSceneToolWindow.UpdateViewAsync();

            var _reviewService = await VS.GetMefServiceAsync<IReviewService>();

            _commitBaselineService.OnCommitBaselineChanged(newBaseline);
            await _reviewService.DeltaReviewOpenDocsAsync();
        }
        else
        {
            _logger.Debug("Baseline unchanged, doing nothing.");
        }
    }



    private void SendTelemetry(string eventName, Dictionary<string, object> additionalData = null)
    {
        Task.Run(async () =>
        {
            var telemetryManager = await VS.GetMefServiceAsync<ITelemetryManager>();
            telemetryManager.SendTelemetry(eventName, additionalData);
        }).FireAndForget();
    }
}
