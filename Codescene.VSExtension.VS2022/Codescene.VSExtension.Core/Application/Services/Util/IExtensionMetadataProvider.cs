namespace Codescene.VSExtension.Core.Application.Services.Util
{
    public interface IExtensionMetadataProvider
    {
        string GetVersion();
        string GetDisplayName();
        string GetDescription();
        string GetPublisher();
    }
}
