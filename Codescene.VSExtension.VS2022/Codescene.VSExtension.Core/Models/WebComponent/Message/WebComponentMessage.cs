namespace Codescene.VSExtension.Core.Models.WebComponent
{
    public class WebComponentMessage<T>
    {
        public string MessageType { get; set; }
        public WebComponentPayload<T> Payload { get; set; }
    }
}
