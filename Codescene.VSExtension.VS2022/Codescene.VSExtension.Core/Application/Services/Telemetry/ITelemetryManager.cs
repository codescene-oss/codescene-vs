using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.Telemetry
{
    public interface ITelemetryManager
    {
        bool IsTelemetryEnabled();
        Task SendTelemetryAsync(string eventData);
        string GetExtensionVersion();
    }
}
