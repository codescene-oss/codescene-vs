using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using System;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.Application.Telemetry
{
    [Export(typeof(IDeviceIdStore))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class DeviceIdStore : IDeviceIdStore
    {
        private readonly ILogger _logger;
        private readonly ICliExecutor _cli;

        private string _deviceId;

        [ImportingConstructor]
        public DeviceIdStore(ILogger logger, ICliExecutor cli)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cli = cli ?? throw new ArgumentNullException(nameof(cli));
        }

        public string GetDeviceId()
        {
            if (!string.IsNullOrEmpty(_deviceId))
                return _deviceId;

            try
            {
                _deviceId = _cli.GetDeviceId();
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to fetch device ID: {ex.Message}");
                _deviceId = "";
            }

            return _deviceId ?? "";
        }
    }
}