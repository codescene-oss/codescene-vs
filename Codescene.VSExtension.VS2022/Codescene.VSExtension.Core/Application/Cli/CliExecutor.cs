// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Util;
using static Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ICliExecutor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public partial class CliExecutor : ICliExecutor
    {
        private readonly ILogger _logger;
        private readonly IIdeServerClient _client;
        private readonly ICacheStorageService _cacheStorage;
        private readonly ISettingsProvider _settingsProvider;
        private readonly Lazy<ITelemetryManager> _telemetryManagerLazy;

        [ImportingConstructor]
        public CliExecutor(
            ILogger logger,
            IIdeServerClient client,
            ICacheStorageService cacheStorage,
            ISettingsProvider settingsProvider,
            [Import(AllowDefault = true)] Lazy<ITelemetryManager> telemetryManagerLazy = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _cacheStorage = cacheStorage ?? throw new ArgumentNullException(nameof(cacheStorage));
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
            _telemetryManagerLazy = telemetryManagerLazy;
        }

        public Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default)
        {
            return TryReadServerValueAsync(
                async token => await _client.DeviceIdAsync(token),
                "Could not get device ID",
                cancellationToken);
        }

        public Task<string> GetFileVersionAsync(CancellationToken cancellationToken = default)
        {
            return TryReadServerValueAsync(
                async token =>
                {
                    var metadata = await _client.StartAsync(token);
                    return metadata?.Sha;
                },
                "Could not get CLI version",
                cancellationToken);
        }

        private async Task<string> TryReadServerValueAsync(
            Func<CancellationToken, Task<string>> read,
            string errorMessage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return (await read(cancellationToken))?.Trim() ?? string.Empty;
            }
            catch (Exception e)
            {
                _logger.Error(errorMessage, e);
                return string.Empty;
            }
        }

        private ITelemetryManager GetTelemetryManager()
        {
            try
            {
                return _telemetryManagerLazy?.Value;
            }
            catch
            {
                return null;
            }
        }

        private void SendPerformanceTelemetry(PerformanceTelemetryData telemetryData)
        {
            var telemetryManager = GetTelemetryManager();
            Task.Run(async () =>
            {
                try
                {
                    await PerformanceTelemetryHelper.SendPerformanceTelemetryAsync(telemetryManager, _logger, telemetryData);
                }
                catch (Exception e)
                {
                    _logger?.Debug($"Failed to send performance telemetry asynchronously: {e.Message}");
                }
            });
        }

        private async Task<(T Result, long ElapsedMs)> ExecuteWithTimingAndLoggingAsync<T>(string label, Func<Task<T>> execute, string errorMessage)
        {
            long elapsedMs = 0;
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await execute();
                stopwatch.Stop();
                elapsedMs = stopwatch.ElapsedMilliseconds;

                _logger.Debug($"{Titles.CODESCENE} {label} completed in {elapsedMs} ms.");
                return (result, elapsedMs);
            }
            catch (DevtoolsException e)
            {
                if (CliExceptionWarnLogging.ShouldLogAsWarning(e))
                {
                    _logger.Warn(CliExceptionWarnLogging.FormatWarningMessage(e, errorMessage));
                }
                else
                {
                    _logger.Error(errorMessage, e);
                }

                throw;
            }
            catch (OperationCanceledException)
            {
                return (default, 0);
            }
            catch (Exception e)
            {
                _logger.Error(errorMessage, e);
                return (default, elapsedMs);
            }
        }
    }
}
