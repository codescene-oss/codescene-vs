using Codescene.VSExtension.Core.Application.Services.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.Telemetry
{
    [Export(typeof(ITelemetryManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class TelemetryManager : ITelemetryManager
    {
        public string GetExtensionVersion()
        {
            throw new NotImplementedException();
        }

        public bool IsTelemetryEnabled()
        {
            const string keyPath = @"Software\Microsoft\VisualStudio\Telemetry";
            const string valueName = "OptIn";

            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath);
            if (key != null)
            {
                var optInValue = key.GetValue(valueName);
                return optInValue is int intVal && intVal == 1;
            }

            return false; // Default to off
        }

        public async Task SendTelemetryAsync(string eventName, Dictionary<string, object> eventData)
        {
            if (!IsTelemetryEnabled())
            {
                return;
            }

            /*
             * create TelemetryEvent object
             * get device-id from DeviceIdStore (to be added)
             * make send telemetry call with cli
            */
        }
    }
}
