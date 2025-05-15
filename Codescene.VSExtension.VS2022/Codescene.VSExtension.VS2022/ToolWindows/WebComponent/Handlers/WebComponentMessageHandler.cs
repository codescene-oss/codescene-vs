using Codescene.VSExtension.Core.Models.WebComponent;
using Community.VisualStudio.Toolkit;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;
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

        if (msgType == WebComponentConstants.MessageTypes.COPY_CODE)
        {
            var handler = await VS.GetMefServiceAsync<CopyRefactoredCodeHandler>();
            handler.CopyToRefactoredCodeToClipboard();
            return;
        }

        if (msgType == WebComponentConstants.MessageTypes.SHOW_DIFF)
        {
            var handler = await VS.GetMefServiceAsync<ShowDiffHandler>();
            await handler.ShowDiffWindowAsync();
            return;
        }

        if (msgType == WebComponentConstants.MessageTypes.APPLY)
        {
            var applier = await VS.GetMefServiceAsync<RefactoringChangesApplier>();
            await applier.ApplyAsync();
            return;
        }

        if (msgType == WebComponentConstants.MessageTypes.REJECT)
        {
            if (_control.CloseRequested is not null)
            {
                await _control.CloseRequested();
            }
            return;
        }
    }
}
