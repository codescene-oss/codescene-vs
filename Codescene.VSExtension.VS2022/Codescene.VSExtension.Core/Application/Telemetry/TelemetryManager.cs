using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Util;

namespace Codescene.VSExtension.Core.Application.Telemetry
{
    [Export(typeof(ITelemetryManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class TelemetryManager : ITelemetryManager
    {
        private readonly ILogger _logger;
        private readonly IProcessExecutor _executor;
        private readonly IDeviceIdStore _deviceIdStore;
        private readonly ICliCommandProvider _cliCommandProvider;
        private readonly IExtensionMetadataProvider _extensionMetadataProvider;

        [ImportingConstructor]
        public TelemetryManager(
            ILogger logger,
            IProcessExecutor executor,
            IDeviceIdStore deviceIdStore,
            ICliCommandProvider cliCommandProvider,
            IExtensionMetadataProvider extensionMetadataProvider)
        {
            _logger = logger;
            _executor = executor;
            _deviceIdStore = deviceIdStore;
            _cliCommandProvider = cliCommandProvider;
            _extensionMetadataProvider = extensionMetadataProvider;
        }

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
                    _extensionMetadataProvider.GetVersion(),
                    additionalEventData);
                var arguments = _cliCommandProvider.SendTelemetryCommand(eventJson);

                var result = _executor.Execute(arguments, null, Constants.Timeout.TELEMETRYTIMEOUT);
            }
            catch (Exception e)
            {
                _logger.Debug($"Unable to send telemetry event: {e.Message}");
            }
        }

        public void SendErrorTelemetry(Exception ex, string context, Dictionary<string, object> extraData = null)
        {
            if (!TelemetryUtils.IsTelemetryEnabled(_logger)) return;
            if (!ErrorTelemetryUtils.ShouldSendError(ex)) return;

            try
            {
                var errorData = ErrorTelemetryUtils.SerializeException(ex, context);

                if (extraData != null)
                {
                    foreach (var kvp in extraData)
                    {
                        errorData[kvp.Key] = kvp.Value;
                    }
                }

                SendTelemetry(Constants.Telemetry.UNHANDLEDERROR, errorData);
                ErrorTelemetryUtils.IncrementErrorCount();
            }
            catch (Exception e)
            {
                _logger.Debug($"Unable to send error telemetry: {e.Message}");
            }
        }
    }
}
