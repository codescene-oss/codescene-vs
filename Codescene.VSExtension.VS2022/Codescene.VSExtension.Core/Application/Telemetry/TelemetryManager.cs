// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models.Cli.Telemetry;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Application.Telemetry
{
    [Export(typeof(ITelemetryManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class TelemetryManager : ITelemetryManager
    {
        private readonly ILogger _logger;
        private readonly IIdeServerHost _host;
        private readonly IDeviceIdStore _deviceIdStore;
        private readonly IExtensionMetadataProvider _extensionMetadataProvider;

        [ImportingConstructor]
        public TelemetryManager(
            ILogger logger,
            IIdeServerHost host,
            IDeviceIdStore deviceIdStore,
            IExtensionMetadataProvider extensionMetadataProvider)
        {
            _logger = logger;
            _host = host;
            _deviceIdStore = deviceIdStore;
            _extensionMetadataProvider = extensionMetadataProvider;
        }

        public async Task SendTelemetryAsync(string eventName, Dictionary<string, object> additionalEventData = null, CancellationToken cancellationToken = default)
        {
            if (!TelemetryUtils.IsTelemetryEnabled(_logger, _extensionMetadataProvider.GetEditorVersion()))
            {
                return;
            }

            try
            {
                var client = _host.Client;
                if (client == null)
                {
                    _logger.Debug("Unable to send telemetry event: IDE server is not running.");
                    return;
                }

                var eventJson = TelemetryUtils.GetTelemetryEventJson(
                    eventName,
                    await _deviceIdStore.GetDeviceIdAsync(cancellationToken),
                    _extensionMetadataProvider.GetVersion(),
                    _extensionMetadataProvider.GetEditorVersion(),
                    additionalEventData);
                var telemetryEvent = JsonConvert.DeserializeObject<TelemetryEvent>(eventJson) ?? new TelemetryEvent();
                await client.TelemetryAsync(telemetryEvent, cancellationToken);
            }
            catch (Exception e)
            {
                _logger.Debug($"Unable to send telemetry event: {e.Message}");
            }
        }

        public async Task SendErrorTelemetryAsync(Exception ex, string context, Dictionary<string, object> extraData = null, CancellationToken cancellationToken = default)
        {
            if (!TelemetryUtils.IsTelemetryEnabled(_logger, _extensionMetadataProvider.GetEditorVersion()))
            {
                return;
            }

            if (!ErrorTelemetryUtils.ShouldSendError(ex))
            {
                return;
            }

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

                await SendTelemetryAsync(Consts.Constants.Telemetry.UNHANDLEDERROR, errorData, cancellationToken);
                ErrorTelemetryUtils.IncrementErrorCount();
            }
            catch (Exception e)
            {
                _logger.Debug($"Unable to send error telemetry: {e.Message}");
            }
        }
    }
}
