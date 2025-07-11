﻿using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;
using Codescene.VSExtension.VS2022.Util;
using Community.VisualStudio.Toolkit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
internal class WebComponentMessageHandler
{
    private ILogger _logger;
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
        switch (msgObject?.MessageType)
        {
            case WebComponentConstants.MessageTypes.INIT:
                return;


            case WebComponentConstants.MessageTypes.GOTO_FUNCTION_LOCATION:
                var payload = msgObject.Payload.ToObject<GotoFunctionLocationPayload>();
                _logger.Info($"Handling '{WebComponentConstants.MessageTypes.GOTO_FUNCTION_LOCATION}' event for {payload.FileName}.");

                await DocumentNavigator.OpenFileAndGoToLineAsync(payload.FileName, payload.Fn.Range.StartLine, _logger);

                return;

            default:
                logger.Debug($" Unable to process webview message, unknown message type: {msgObject.MessageType}.");
                return;
        }
    }
}
