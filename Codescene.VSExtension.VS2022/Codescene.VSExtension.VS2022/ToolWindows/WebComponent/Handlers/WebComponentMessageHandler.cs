using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Codescene.VSExtension.Core.Application.Ace;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.WebComponent.Model;
using Codescene.VSExtension.VS2022.Application.Services;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Codescene.VSExtension.Core.Consts.WebComponentConstants;

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

                case MessageTypes.COPYCODE:
                    await HandleCopyCodeAsync(msgObject, logger);
                    break;

                case MessageTypes.SHOWDIFF:
                    await HandleShowDiffAsync();
                    break;

                case MessageTypes.APPLY:
                    await HandleApplyAsync(msgObject, logger);
                    break;

                case MessageTypes.REJECT:
                    await HandleRejectAsync();
                    break;

                case MessageTypes.RETRY:
                    await HandleRetryRefactoring(msgObject, logger);
                    break;

                case MessageTypes.GOTOFUNCTIONLOCATION:
                    await HandleGotoFunctionLocationAsync(msgObject, logger);
                    break;

                case MessageTypes.OPENDOCSFORFUNCTION:
                    await HandleOpenDocsForFunctionAsync(msgObject, logger);
                    break;

                case MessageTypes.CANCEL:
                    await HandleCancelAsync();
                    break;

                case MessageTypes.CLOSE:
                    await HandleCloseAsync();
                    break;

                case MessageTypes.OPENSETTINGS:
                    await HandleOpenSettingsAsync();
                    break;

                case MessageTypes.ACKNOWLEDGED:
                    await HandleAcknowledgedAsync(msgObject, logger);
                    break;

                case MessageTypes.REQUESTANDPRESENTREFACTORING:
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
        if (string.IsNullOrEmpty(source))
        {
            return;
        }

        logger.Debug($"Webview '{source}' is ready to take messages.");

        // Mark the window as initialized, which will process any pending messages
        await _control.MarkAsInitializedAsync();
    }

    private async Task HandleCopyCodeAsync(MessageObj<JToken> msgObject, ILogger logger)
    {
        var payload = msgObject.Payload.ToObject<CopyCodePayload>();
        HandleAceTelemetry(Constants.Telemetry.ACEREFACTORCOPYCODE);

        if (payload.Code == null)
        {
            logger.Warn("Cannot copy refactored code as it is undefined.");
            return;
        }

        logger.Info($"Copied refactored code from function '{payload.Fn.Name}'.");
        Clipboard.SetText(payload.Code);
    }

    private async Task HandleShowDiffAsync()
    {
        HandleAceTelemetry(Constants.Telemetry.ACEREFACTORDIFFSHOWN);

        var diffHandler = await VS.GetMefServiceAsync<ShowDiffHandler>();
        await diffHandler.ShowDiffWindowAsync();
    }

    private async Task HandleApplyAsync(MessageObj<JToken> msgObject, ILogger logger)
    {
        var payload = msgObject.Payload.ToObject<ApplyPayload>();
        HandleAceTelemetry(Constants.Telemetry.ACEREFACTORAPPLIED);

        var applier = await VS.GetMefServiceAsync<RefactoringChangesApplier>();
        await applier.ApplyAsync(payload);

        if (_control.CloseRequested is not null)
        {
            await _control.CloseRequested();
        }

        // Refresh the view after applying changes, because of the bug with two methods with the same name, needs to be revalidated.
        await CodeSceneToolWindow.UpdateViewAsync();
    }

    private async Task HandleRetryRefactoring(MessageObj<JToken> msgObject, ILogger logger)
    {
        var payload = msgObject.Payload.ToObject<RetryPayload>();

        logger.Info($"Triggered retry refactoring for '{payload.FnToRefactor.Name}'.");

        var onClickRefactoringHandler = await VS.GetMefServiceAsync<OnClickRefactoringHandler>();
        await onClickRefactoringHandler.HandleAsync(
            payload.FilePath,
            payload.FnToRefactor,
            AceConstants.AceEntryPoint.RETRY);
    }

    private async Task HandleRejectAsync()
    {
        HandleAceTelemetry(Constants.Telemetry.ACEREFACTORREJECTED);

        if (_control.CloseRequested is not null)
        {
            await _control.CloseRequested();
        }
    }

    private void HandleAceTelemetry(string telemetryEvent)
    {
        var additionalData = new Dictionary<string, object>
        {
            { "traceId", AceManager.LastRefactoring.Refactored.TraceId },
            { "skipCache ", false },
        };
        SendTelemetry(telemetryEvent, additionalData);
    }

    private async Task HandleCancelAsync()
    {
        var onClickRefactoringHandler = await VS.GetMefServiceAsync<OnClickRefactoringHandler>();

        if (onClickRefactoringHandler != null)
        {
            onClickRefactoringHandler.HandleCancel();
        }

        if (_control.CloseRequested is not null)
        {
            await _control.CloseRequested();
        }
    }

    private async Task HandleGotoFunctionLocationAsync(MessageObj<JToken> msgObject, ILogger logger)
    {
        var payload = msgObject.Payload.ToObject<GotoFunctionLocationPayload>();
        var startLine = payload.Fn?.Range?.StartLine ?? 1;  // When opening files without focus on specific line, Fn is null.

        await DocumentNavigator.OpenFileAndGoToLineAsync(
            payload.FileName,
            startLine,
            logger);
    }

    private async Task HandleOpenDocsForFunctionAsync(MessageObj<JToken> msgObject, ILogger logger)
    {
        var payload = msgObject.Payload.ToObject<OpenDocsForFunctionPayload>();
        _logger.Debug($"Opening '{payload.DocType}'...");

        var category = DocumentationMappings.DocNameMap[payload.DocType] ?? string.Empty;
        var fn = await AceUtils.GetRefactorableFunctionAsync(new GetRefactorableFunctionsModel
        {
            Path = payload.FileName,
            Category = category,
            FunctionRange = payload.Fn?.Range,
        });

        await _showDocsHandler?.HandleAsync(
        new ShowDocumentationModel(
            payload.FileName,
            payload.DocType,
            payload.Fn?.Name,
            payload.Fn?.Range),
        fn,
        DocsEntryPoint.CodeHealthMonitor);
    }

    private async Task HandleRequestAndPresentRefactoringAsync(MessageObj<JToken> msgObject, ILogger logger)
    {
        var payload = msgObject.Payload.ToObject<RequestAndPresentRefactoringPayload>();
        var onClickRefactoringHandler = await VS.GetMefServiceAsync<OnClickRefactoringHandler>();

        logger.Debug($"Requesting refactoring for function '{payload.Fn.Name}' in file '{payload.FileName}' from '{payload.Source}' view.");

        if (payload.FnToRefactor == null)
        {
            logger.Warn($"Function to refactor not found. Cannot proceed with refactoring.");
            return;
        }

        if (payload.Source == ViewTypes.DOCS && _control.CloseRequested is not null)
        {
            await _control.CloseRequested();
        }

        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        await onClickRefactoringHandler.HandleAsync(
            payload.FileName,
            payload.FnToRefactor,
            AceConstants.AceEntryPoint.CODEVISION);
    }

    private async Task HandleOpenSettingsAsync()
    {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        await VS.Settings.OpenAsync<OptionsProvider.GeneralOptions>();

        SendTelemetry(Constants.Telemetry.OPENSETTINGS);
    }

    // Currently, we only receive this message from the ACE view if the content is marked as 'stale'.
    private async Task HandleCloseAsync()
    {
        if (_control.CloseRequested is not null)
        {
            await _control.CloseRequested();
        }
    }

    private async Task HandleAcknowledgedAsync(MessageObj<JToken> msgObject, ILogger logger)
    {
        var acknowledgementStateService = await VS.GetMefServiceAsync<AceAcknowledgementStateService>();
        acknowledgementStateService.SetAcknowledged();

        _logger?.Info("ACE usage acknowledged.");

        var payload = msgObject.Payload.ToObject<AceAcknowledgePayload>();
        if (payload.FnToRefactor == null)
        {
            logger.Info("Current code smell is not refactorable.");

            if (payload.Source == ViewTypes.DOCS)
            {
                logger.Debug("Refreshing 'docs' tool window to disable the refactoring button.");
                await CodeSmellDocumentationWindow.RefreshViewAsync();
            }

            return;
        }

        _logger?.Info($"Refactoring function '{payload.FnToRefactor.Name}'...");

        // Close the acknowledgement window
        if (_control.CloseRequested is not null)
        {
            await _control.CloseRequested();
        }

        var onClickRefactoringHandler = await VS.GetMefServiceAsync<OnClickRefactoringHandler>();
        await onClickRefactoringHandler.HandleAsync(
            payload.FilePath,
            payload.FnToRefactor,
            AceConstants.AceEntryPoint.ACEACKNOWLEDGEMENT);
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
