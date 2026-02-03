namespace Codescene.VSExtension.VS2022.ToolWindows.WebComponent.Handlers;

internal class MessageObj<T>
{
    public string MessageType { get; set; }
    public T Payload { get; set; }
}
