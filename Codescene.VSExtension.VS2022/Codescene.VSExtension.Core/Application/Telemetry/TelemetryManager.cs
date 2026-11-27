// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json;

namespace Codescene.VSExtension.Core.Application.Telemetry
{
    [Export(typeof(ITelemetryManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class TelemetryManager : ITelemetryManager
    {
        private readonly ILogger _logger;
        private readonly IIdeServerClient _client;
        private readonly IDeviceIdStore _deviceIdStore;
        private readonly IExtensionMetadataProvider _extensionMetadataProvider;
        private readonly ISettingsProvider _settingsProvider;
        private readonly ITelemetryRepositoryPaths _repositoryPaths;
        private readonly TelemetryRoutingGate<QueuedEvent> _gate;
        private readonly ConcurrentDictionary<Task, byte> _inFlight = new ConcurrentDictionary<Task, byte>();
        private readonly string _sessionId = Guid.NewGuid().ToString();

        [ImportingConstructor]
        public TelemetryManager(
            ILogger logger,
            IIdeServerClient client,
            IDeviceIdStore deviceIdStore,
            IExtensionMetadataProvider extensionMetadataProvider,
            [Import(AllowDefault = true)] ISettingsProvider settingsProvider = null,
            [Import(AllowDefault = true)] ITelemetryRepositoryPaths repositoryPaths = null)
            : this(logger, client, deviceIdStore, extensionMetadataProvider, settingsProvider, repositoryPaths, null)
        {
        }

        internal TelemetryManager(
            ILogger logger,
            IIdeServerClient client,
            IDeviceIdStore deviceIdStore,
            IExtensionMetadataProvider extensionMetadataProvider,
            ISettingsProvider settingsProvider,
            ITelemetryRepositoryPaths repositoryPaths,
            TimeSpan? routingBound)
        {
            _logger = logger;
            _client = client;
            _deviceIdStore = deviceIdStore;
            _extensionMetadataProvider = extensionMetadataProvider;
            _settingsProvider = settingsProvider;
            _repositoryPaths = repositoryPaths;
            _gate = new TelemetryRoutingGate<QueuedEvent>(
                item => _ = TrackAsync(item, CancellationToken.None),
                item => item.Name == Constants.Telemetry.SESSIONSTARTED,
                routingBound);
        }

        internal string SessionId => _sessionId;

        /// <summary>
        /// Sends a telemetry event with the specified event name and optional additional data.
        /// </summary>
        /// <remarks>
        /// This method builds a telemetry event JSON payload that includes the device ID,
        /// extension version, and any additional data provided. It then sends the event via
        /// a CLI command, using a defined timeout. If telemetry is disabled or an error occurs,
        /// the method logs the issue and returns silently.
        /// </remarks>
        public async Task SendTelemetryAsync(string eventName, Dictionary<string, object> additionalEventData = null, CancellationToken cancellationToken = default)
        {
            if (!IsTelemetryAllowed())
            {
                return;
            }

            var item = new QueuedEvent(eventName, additionalEventData, DateTime.UtcNow.ToString("o"));
            if (_gate.TryEnqueue(item))
            {
                return;
            }

            await TrackAsync(item, cancellationToken).ConfigureAwait(false);
        }

        public int HoldRouting() => _gate.Hold();

        public void RoutingReady(int generation) => _gate.Ready(generation);

        public async Task EndSessionAsync(TimeSpan timeout)
        {
            if (IsTelemetryAllowed())
            {
                var item = new QueuedEvent(Constants.Telemetry.SESSIONENDED, null, DateTime.UtcNow.ToString("o"));
                if (!_gate.TryEnqueue(item))
                {
                    _ = TrackAsync(item, CancellationToken.None);
                }
            }

            _gate.Close();

            var pending = _inFlight.Keys.ToList();
            if (pending.Count == 0)
            {
                return;
            }

            await Task.WhenAny(Task.WhenAll(pending), Task.Delay(timeout)).ConfigureAwait(false);
        }

        private static IReadOnlyList<string> ToPathList(object value)
        {
            switch (value)
            {
                case string single:
                    return string.IsNullOrEmpty(single) ? null : new[] { single };
                case IEnumerable<string> many:
                    return many.Where(p => !string.IsNullOrEmpty(p)).ToList();
                default:
                    return null;
            }
        }

        private Task TrackAsync(QueuedEvent item, CancellationToken cancellationToken)
        {
            var task = PostAsync(item, cancellationToken);
            _inFlight.TryAdd(task, 0);
            _ = task.ContinueWith(t => _inFlight.TryRemove(t, out _), TaskScheduler.Default);
            return task;
        }

        private async Task PostAsync(QueuedEvent item, CancellationToken cancellationToken)
        {
            try
            {
                if (!IsTelemetryAllowed())
                {
                    return;
                }

                var data = item.Data == null ? null : new Dictionary<string, object>(item.Data);
                var repositoryPaths = await ResolveRepositoryPathsAsync(data).ConfigureAwait(false);

                var telemetryEvent = TelemetryUtils.CreateTelemetryEvent(
                    item.Name,
                    await _deviceIdStore.GetDeviceIdAsync(cancellationToken),
                    _extensionMetadataProvider.GetVersion(),
                    _extensionMetadataProvider.GetEditorVersion());
                telemetryEvent.EventTime = item.EventTime;
                telemetryEvent.SessionId = _sessionId;

                string eventJson = TelemetryUtils.GetTelemetryEventJson(telemetryEvent, data, repositoryPaths);
                if (string.IsNullOrEmpty(eventJson))
                {
                    return;
                }

                var eventPayload = JsonConvert.DeserializeObject(eventJson);
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeoutCts.CancelAfter(Constants.Timeout.TELEMETRYTIMEOUT);
                    await _client.TelemetryAsync(eventPayload, timeoutCts.Token);
                }
            }
            catch (Exception e)
            {
                _logger.Debug($"Unable to send telemetry event: {e.Message}");
            }
        }

        private async Task<IReadOnlyList<string>> ResolveRepositoryPathsAsync(Dictionary<string, object> data)
        {
            if (data != null && data.TryGetValue(Constants.Telemetry.REPOSITORYPATHS, out var explicitPaths))
            {
                data.Remove(Constants.Telemetry.REPOSITORYPATHS);
                return ToPathList(explicitPaths);
            }

            if (_repositoryPaths == null)
            {
                return null;
            }

            try
            {
                return await _repositoryPaths.GetWorkspaceRepositoryPathsAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.Debug($"Unable to resolve telemetry repository paths: {e.Message}");
                return null;
            }
        }

        private bool IsTelemetryAllowed()
        {
            if (TelemetryUtils.TelemetryEnabledOverrideForTests.HasValue)
            {
                return TelemetryUtils.TelemetryEnabledOverrideForTests.Value;
            }

            if (_settingsProvider != null && !_settingsProvider.EnableTelemetry)
            {
                return false;
            }

            return TelemetryUtils.IsTelemetryEnabled(_logger, _extensionMetadataProvider.GetEditorVersion());
        }

        private sealed class QueuedEvent
        {
            public QueuedEvent(string name, Dictionary<string, object> data, string eventTime)
            {
                Name = name;
                Data = data;
                EventTime = eventTime;
            }

            public string Name { get; }

            public Dictionary<string, object> Data { get; }

            public string EventTime { get; }
        }
    }
}
