using Codescene.VSExtension.Core.Models.WebComponent.Payload;

namespace Codescene.VSExtension.Core.Models.WebComponent.Message
{
    public class WebComponentMessage<T>
    {
        public string MessageType { get; set; }
        public WebComponentPayload<T> Payload { get; set; }
    }
}
