namespace Codescene.VSExtension.Core.Application.Services.Settings
{
    public interface ISettingsProvider
    {
        bool ShowDebugLogs { get; }
        string AuthToken { get; }
    }
}