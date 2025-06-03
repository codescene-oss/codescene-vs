using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;
using Community.VisualStudio.Toolkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
internal class WebComponentMessageHandler
{
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
        ILogger logger = await VS.GetMefServiceAsync<ILogger>();
        MessageObj<JToken> msgObject;
        try
        {
            msgObject = JsonConvert.DeserializeObject<MessageObj<JToken>>(message);
        }
        catch (Exception ex)
        {
            logger.Error($"Unable to process webview message. Deserialization failed.", ex);
            return;
        }

        logger.Debug($"Received message from webview: '{msgObject?.MessageType}'.");

        await ProcessMessageAsync(msgObject, logger);
    }

    private async Task ProcessMessageAsync(MessageObj<JToken> msgObject, ILogger logger)
    {
        switch (msgObject?.MessageType)
        {
            case WebComponentConstants.MessageTypes.INIT:
                return;

            case WebComponentConstants.MessageTypes.COPY_CODE:
                var copyHandler = await VS.GetMefServiceAsync<CopyRefactoredCodeHandler>();
                copyHandler.CopyToRefactoredCodeToClipboard();
                return;

            case WebComponentConstants.MessageTypes.SHOW_DIFF:
                var diffHandler = await VS.GetMefServiceAsync<ShowDiffHandler>();
                await diffHandler.ShowDiffWindowAsync();
                return;

            case WebComponentConstants.MessageTypes.APPLY:
                var applier = await VS.GetMefServiceAsync<RefactoringChangesApplier>();
                await applier.ApplyAsync();
                return;

            case WebComponentConstants.MessageTypes.REJECT:
                if (_control.CloseRequested is not null) await _control.CloseRequested();
                return;

            case WebComponentConstants.MessageTypes.GOTO_FUNCTION_LOCATION:
                //TODO
                var payload = msgObject.Payload.ToObject<GotoFunctionLocationPayload>();
                System.Diagnostics.Debug.WriteLine($"Goto: {payload.Fn.Name} in {payload.FileName}");
                return;

            default:
                logger.Debug($" Unable to process webview message, unknown message type: {msgObject.MessageType}.");
                return;
        }
    }
}
