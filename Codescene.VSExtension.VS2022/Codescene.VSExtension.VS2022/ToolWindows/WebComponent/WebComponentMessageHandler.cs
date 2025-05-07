using Codescene.VSExtension.Core.Models.WebComponent;
using Codescene.VSExtension.VS2022.DocumentEventsHandler;
using Community.VisualStudio.Toolkit;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent;
internal class WebComponentMessageHandler
{

    private readonly WebComponentUserControl _control;
    public WebComponentMessageHandler(WebComponentUserControl control)
    {
        _control = control;
    }

    public async Task HandleAsync(string message)
    {
        var msgObject = JsonConvert.DeserializeObject<MessageObj<string>>(message);
        if (msgObject == null)
        {
            throw new System.ArgumentNullException(nameof(msgObject));
        }

        var msgType = msgObject.MessageType;
        if (msgType == WebComponentConstants.MessageTypes.INIT)
        {
            return;
        }

        if (msgType == WebComponentConstants.MessageTypes.APPLY)
        {
            var applier = await VS.GetMefServiceAsync<RefactoringChangesApplier>();
            applier.Apply();
            return;
        }

        var payload = msgObject.Payload;

        if (payload == "close")
        {
            if (_control.CloseRequested is not null)
            {
                await _control.CloseRequested();
            }
        }
    }
}
