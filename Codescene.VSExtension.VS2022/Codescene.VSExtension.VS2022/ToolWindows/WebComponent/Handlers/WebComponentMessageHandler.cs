using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Models;
using Codescene.VSExtension.VS2022.Util;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using static Codescene.VSExtension.VS2022.Util.LogHelper;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

internal class WebComponentMessageHandler
{
    /// <summary>
    /// Handles messages sent from the WebView component to the native Visual Studio extension.
    /// Processes different message types by dispatching to the appropriate handler services.
    /// </summary>
    public async Task HandleAsync(string message)
    {
        MessageObj<JToken> msgObject;

        try
        {
            msgObject = JsonConvert.DeserializeObject<MessageObj<JToken>>(message);
        }
        catch (Exception ex)
        {
            LogAsync($"Unable to process webview message. Deserialization failed.", LogLevel.Error, ex).FireAndForget();
            return;
        }

        LogAsync($"Received message from webview: '{msgObject?.MessageType}'.", LogLevel.Debug).FireAndForget();

        ProcessMessageAsync(msgObject).FireAndForget();
    }

    private async Task ProcessMessageAsync(MessageObj<JToken> msgObject)
    {
        if (msgObject?.MessageType == null)
        {
            LogAsync("Unable to process webview message: missing MessageType.", LogLevel.Debug).FireAndForget();

            return;
        }

        LogAsync($"Handling '{msgObject.MessageType}' message.", LogLevel.Debug).FireAndForget();

        try
        {
            switch (msgObject?.MessageType)
            {
                case WebComponentConstants.MessageTypes.INIT:
                    break;
                case WebComponentConstants.MessageTypes.GOTO_FUNCTION_LOCATION:
                    await HandleGotoFunctionLocationAsync(msgObject);
                    break;
                default:
                    LogAsync($"Unknown message type: {msgObject.MessageType}.", LogLevel.Debug).FireAndForget();
                    break;
            }
        }
        catch (Exception e)
        {
            LogAsync($"Unable to handle '{msgObject.MessageType}'", LogLevel.Error, e).FireAndForget();
        }
    }

    private async Task HandleGotoFunctionLocationAsync(MessageObj<JToken> msgObject)
    {
        var payload = msgObject.Payload.ToObject<GotoFunctionLocationPayload>();
        var startLine = payload.Fn?.Range?.StartLine ?? 1;  // When opening files without focus on specific line, Fn is null.

        await DocumentNavigator.OpenFileAndGoToLineAsync(
            payload.FileName,
            startLine);
    }
}
