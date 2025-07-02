using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Application.Services.Telemetry
{
    [Export(typeof(ITelemetryManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class TelemetryManager : ITelemetryManager
    {
        private const int TIMEOUT_MS = 5000;
        private static readonly TimeSpan TELEMETRY_TIMEOUT = TimeSpan.FromMilliseconds(TIMEOUT_MS);

        [Import]
        private readonly ILogger _logger;

        [Import]
        private readonly IProcessExecutor _executor;

        public string GetExtensionVersion()
        {
            throw new NotImplementedException();

            /*
             *         await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

        var extensionManager = await VS.Services.GetExtensionManagerAsync();
        var extensions = extensionManager.GetInstalledExtensions();

        // Use the same ID as in your .vsixmanifest
        const string extensionId = "CodesceneVSExtension.c90b6097-3fbd-4b82-a308-f9568074c67a";

        var thisExtension = extensions.FirstOrDefault(ext =>
            ext.Header.Identifier.Equals(extensionId, StringComparison.OrdinalIgnoreCase));

        return thisExtension?.Header.Version?.ToString();             * 
             */
        }

        public bool IsTelemetryEnabled()
        {
            try
            {
                const string keyPath = @"Software\Microsoft\VisualStudio\Telemetry";
                const string valueName = "OptIn";

                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath);

                if (key != null)
                {
                    var optInValue = key.GetValue(valueName);
                    return optInValue is int intVal && intVal == 1;
                }

                return false;
            }
            catch (Exception e)
            {
                _logger.Debug($"Unable to check if telemetry is enabled: {e.Message}");
                return false;
            }
        }

        public async Task SendTelemetryAsync(string eventData)
        {
            if (!IsTelemetryEnabled()) return;

            try
            {
                /*
                 * create TelemetryEvent object
                 * get device-id from DeviceIdStore (to be added)
                 * make send telemetry call with cli
                */

                var arguments = "telemetry --event {}";
                _executor.Execute(arguments, null, TELEMETRY_TIMEOUT);
            }
            catch (Exception e)
            {
                _logger.Debug($"Unable to send telemetry event: {e.Message}");
            }
        }
    }
}
