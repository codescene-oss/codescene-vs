namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

class MessageObj<T>
{
    public string MessageType { get; set; }
    public T Payload { get; set; }
}
