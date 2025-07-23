using Codescene.VSExtension.Core.Application.Services.Cli;
using Codescene.VSExtension.Core.Application.Services.ErrorHandling;
using Codescene.VSExtension.Core.Application.Services.Util;
using Codescene.VSExtension.Core.Models.WebComponent.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.Application.Services.Telemetry
{
    [Export(typeof(ITelemetryManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class TelemetryManager : ITelemetryManager
    {
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
        /// Sends a telemetry event with the specified event name and optional additional data.
        /// </summary>
        /// <remarks>
        /// This method builds a telemetry event JSON payload that includes the device ID,
        /// extension version, and any additional data provided. It then sends the event via
        /// a CLI command, using a defined timeout. If telemetry is disabled or an error occurs,
        /// the method logs the issue and returns silently.
        /// </remarks>
        public void SendTelemetry(string eventName, Dictionary<string, object> additionalEventData = null)
        {
            if (!TelemetryUtils.IsTelemetryEnabled(_logger)) return;

            try
            {
                string eventJson = TelemetryUtils.GetTelemetryEventJson(
                    eventName,
                    _deviceIdStore.GetDeviceId(),
                    $"{_extensionMetadataProvider.GetVersion()}-premium",
                    additionalEventData);
                var arguments = _cliCommandProvider.SendTelemetryCommand(eventJson);

                var result = _executor.Execute(arguments, null, Constants.Timeout.TELEMETRY_TIMEOUT);
            }
            catch (Exception e)
            {
                _logger.Debug($"Unable to send telemetry event: {e.Message}");
            }
        }
    }
}