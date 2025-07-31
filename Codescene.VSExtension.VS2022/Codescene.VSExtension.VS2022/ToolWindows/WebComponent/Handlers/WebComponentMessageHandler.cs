using Codescene.VSExtension.Core.Application.Services.Cache.Review;
using Codescene.VSExtension.Core.Application.Services.Cache.Review.Model.AceRefactorableFunctions;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Telemetry;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Codescene.VSExtension.Core.Models.WebComponent.WebComponentConstants;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

internal class WebComponentMessageHandler
{
    private ILogger _logger;

    private ShowDocumentationHandler _showDocsHandler;

    private readonly WebComponentUserControl _control;

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

                case MessageTypes.CANCEL:
                    await HandleCancelAsync();
                    break;

                case MessageTypes.REQUEST_AND_PRESENT_REFACTORING:
                    await HandleRequestAndPresentRefactoringAsync(msgObject, logger);
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
        if (source == ViewTypes.ACE)
        {
            await AceToolWindow.UpdateViewAsync().ConfigureAwait(false);
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
        if (_control.CloseRequested is not null)
            await _control.CloseRequested();
        // Refresh the view after applying changes, because of the bug with two methods with the same name 
        await CodeSceneToolWindow.UpdateViewAsync();
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

    private async Task HandleCancelAsync()
    {
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
        _logger.Debug($"Payload '{JsonConvert.SerializeObject(payload)}'...");

        await _showDocsHandler?.HandleAsync(
        new ShowDocumentationModel(
            payload.FileName,
            payload.DocType,
            payload.Fn?.Name,
            payload.Fn?.Range), DocsEntryPoint.CodeHealthMonitor
        );
    }

    private async Task HandleRequestAndPresentRefactoringAsync(MessageObj<JToken> msgObject, ILogger logger)
    {
        var payload = msgObject.Payload.ToObject<RequestAndPresentRefactoringPayload>();

        logger.Debug($"Requesting refactoring for function '{payload.Fn.Name}' in file '{payload.FileName}'.");

        var onClickRefactoringHandler = await VS.GetMefServiceAsync<OnClickRefactoringHandler>();

        var cache = new AceRefactorableFunctionsCacheService();

        var docView = await VS.Documents.OpenAsync(payload.FileName);
        if (docView?.TextBuffer is not ITextBuffer buffer)
            return;

        var content = buffer.CurrentSnapshot.GetText();

        var refactorableFunctions = cache.Get(new AceRefactorableFunctionsQuery(
            payload.FileName,
            content
        ));

        logger.Debug($"Found {refactorableFunctions.Count} refactorable functions in file '{payload.FileName}'.");

        await onClickRefactoringHandler.HandleAsync(
            payload.FileName,
            refactorableFunctions.FirstOrDefault(fn => fn.Name == payload.Fn.Name)
        );
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
