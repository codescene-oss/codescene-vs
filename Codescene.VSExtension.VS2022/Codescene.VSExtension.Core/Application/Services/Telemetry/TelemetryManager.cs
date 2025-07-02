using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

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

        [Import]
        private readonly IDeviceIdStore _deviceIdStore;

        [Import]
        private readonly ICliCommandProvider _cliCommandProvider;

        [Import]
        private readonly IExtensionMetadataProvider _extensionMetadataProvider;

        /// <summary>
        /// Checks if the user has opted in to the Visual Studio Customer Experience Improvement Program (VSCEIP) telemetry.
        /// By relying on this official opt-in status, our extension respects the user's choice regarding telemetry.
        /// </summary>
        /// <remarks>
        /// Visual Studio 2022 stores telemetry opt-in status in the registry key:
        /// <c>HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\VSCommon\17.0\SQM</c>
        /// under the DWORD value <c>OptIn</c>.
        /// 
        /// Value meanings:
        /// - 1: User has opted in to telemetry collection (enabled)
        /// - 0: User has opted out of telemetry collection (disabled)
        ///
        /// For more information, see:
        /// https://learn.microsoft.com/en-us/visualstudio/ide/visual-studio-experience-improvement-program?view=vs-2022
        /// </remarks>
        /// <returns>True if telemetry is enabled (opted in); otherwise, false.</returns>
        public bool IsTelemetryEnabled()
        {
            try
            {
                const string keyPath = @"SOFTWARE\Wow6432Node\Microsoft\VSCommon\17.0\SQM";
                const string valueName = "OptIn";

                var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);

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

        public void SendTelemetryAsync(string eventName, Dictionary<string, object> eventData = null)
        {
            if (!IsTelemetryEnabled()) return;

            try
            {
                var telemetryEvent = new TelemetryEvent
                {
                    Internal = false,
                    EventName = eventName,
                    UserId = _deviceIdStore.GetDeviceId(),
                    EditorType = Constants.Telemetry.SOURCE_IDE,
                    ExtensionVersion = _extensionMetadataProvider.GetVersion(),
                };

                var eventJson = JsonConvert.SerializeObject(telemetryEvent);
                var arguments = _cliCommandProvider.SendTelemetryCommand(eventJson);

                //_executor.Execute(arguments, null, TELEMETRY_TIMEOUT);
            }
            catch (Exception e)
            {
                _logger.Debug($"Unable to send telemetry event: {e.Message}");
            }
        }
    }
}
