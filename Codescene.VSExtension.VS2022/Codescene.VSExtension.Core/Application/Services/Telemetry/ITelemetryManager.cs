using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.Telemetry
{
    public interface ITelemetryManager
    {
        bool IsTelemetryEnabled();
        Task SendTelemetryAsync(string eventName, Dictionary<string, object> eventData);
        string GetExtensionVersion();
    }
}
