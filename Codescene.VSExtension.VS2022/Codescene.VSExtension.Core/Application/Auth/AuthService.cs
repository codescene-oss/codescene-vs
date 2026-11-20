// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Interfaces.Util;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Auth
{
    [Export(typeof(IAuthService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed partial class AuthService : IAuthService
    {
        private readonly IIdeServerClient _client;
        private readonly IIdeServerAuthClient _authClient;
        private readonly IAuthSettings _settings;
        private readonly IAuthHost _host;
        private readonly ITelemetryManager _telemetry;
        private readonly IDebounceService _debounce;
        private readonly IAsyncTaskScheduler _scheduler;
        private readonly ILogger _logger;

        private readonly object _gate = new object();

        private bool _started;
        private bool _firstStartHandled;
        private bool _capable;
        private bool _optedIn;
        private int _configureGeneration;
        private int _loginEpoch;

        private JObject _status;
        private AuthFlowState _flow = AuthFlowState.Idle;
        private AuthSnapshot _current = AuthSnapshot.Inactive;

        [ImportingConstructor]
        public AuthService(
            IIdeServerClient client,
            IIdeServerAuthClient authClient,
            IAuthSettings settings,
            IAuthHost host,
            ITelemetryManager telemetry,
            IDebounceService debounce,
            IAsyncTaskScheduler scheduler,
            ILogger logger)
        {
            _client = client;
            _authClient = authClient;
            _settings = settings;
            _host = host;
            _telemetry = telemetry;
            _debounce = debounce;
            _scheduler = scheduler;
            _logger = logger;
        }

        public event EventHandler<AuthSnapshot> SnapshotChanged;

        public AuthSnapshot Current
        {
            get
            {
                lock (_gate)
                {
                    return _current;
                }
            }
        }

        public void Start()
        {
            lock (_gate)
            {
                if (_started)
                {
                    return;
                }

                _started = true;
            }

            _client.ServerStarted += OnServerStarted;
            _authClient.AuthStatusChanged += OnAuthStatusChanged;
            _settings.Changed += OnSettingsChanged;

            var metadata = _client.Metadata;
            if (metadata != null)
            {
                lock (_gate)
                {
                    if (!_firstStartHandled)
                    {
                        _scheduler.Schedule(() => HandleServerStartAsync(metadata, false));
                    }
                }
            }
        }

        private void OnServerStarted(object sender, Models.Cli.Rpc.ServerStartEvent e)
        {
            if (e == null)
            {
                return;
            }

            bool shouldHandle;
            lock (_gate)
            {
                shouldHandle = e.Restart || !_firstStartHandled;
            }

            if (shouldHandle)
            {
                _scheduler.Schedule(() => HandleServerStartAsync(e.Metadata, e.Restart));
            }
        }

        private void OnAuthStatusChanged(object sender, JObject status)
        {
            if (status == null)
            {
                return;
            }

            _scheduler.Schedule(() => HandleAuthStatusChangedAsync(status));
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            _debounce.Debounce("auth-settings", () => _scheduler.Schedule(RefreshAsync), TimeSpan.FromMilliseconds(500));
        }

        private async Task HandleServerStartAsync(Models.Cli.Rpc.ServerMetadata metadata, bool isRestart)
        {
            lock (_gate)
            {
                _firstStartHandled = true;
                _capable = metadata?.Capabilities?.Contains("auth-v1") ?? false;
                _syncSupported = true;
                _optedIn = false;
            }

            await RefreshAsync().ConfigureAwait(false);
        }

        private async Task HandleAuthStatusChangedAsync(JObject status)
        {
            bool shouldApply;
            lock (_gate)
            {
                shouldApply = _capable && _settings.BrowserSignInEnabled;
            }

            if (shouldApply)
            {
                await ApplyStatusAsync(status).ConfigureAwait(false);
            }
        }

        private async Task RefreshAsync()
        {
            bool capable;
            bool enabled;
            lock (_gate)
            {
                capable = _capable;
                enabled = capable && _settings.BrowserSignInEnabled;
            }

            if (enabled)
            {
                await ConfigureAsync().ConfigureAwait(false);
            }
            else
            {
                await DisableAsync().ConfigureAwait(false);
            }
        }

        private async Task ConfigureAsync()
        {
            int generation;
            lock (_gate)
            {
                _configureGeneration++;
                generation = _configureGeneration;
                _optedIn = true;
                _syncOnSignIn = true;
            }

            var serverUrl = AuthConfiguration.ServerUrl(_settings.ServerUrl);
            var accessToken = AuthConfiguration.AccessToken(_settings.AccessToken);

            JObject status;
            try
            {
                status = await _authClient.AuthConfigureAsync(serverUrl, accessToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                _logger.Warn("[auth] auth-configure failed; keeping the last status");
                return;
            }

            bool shouldApply;
            lock (_gate)
            {
                shouldApply = generation == _configureGeneration;
            }

            if (shouldApply)
            {
                await ApplyStatusAsync(status).ConfigureAwait(false);
            }
        }

        private async Task DisableAsync()
        {
            Action raiseEvent;
            lock (_gate)
            {
                ForgetAccountData();
                _repoSyncing = false;
                _flow = AuthFlowState.Idle;
                _loginEpoch++;
                _status = null;

                var snapshot = _current.WithActive(false).WithStatus(null);
                snapshot = snapshot.WithSurface(AuthConstants.Surfaces.MONITOR).WithStep(AuthConstants.Steps.SUMMARY).WithContext(AuthConstants.Contexts.MANUAL);

                if (_current.Repo != null)
                {
                    snapshot = snapshot.WithRepo(_current.Repo);
                }
                else
                {
                    snapshot = snapshot.WithRepo(null);
                }

                snapshot = snapshot.WithAccounts(null).WithProjects(null).WithLinkedProject(null).WithProjectSource(null).WithProjectReason(null).WithProjectCandidates(new JArray());
                snapshot = snapshot.WithSyncing(false).WithSignInBannerHidden(false);

                if (!_current.Equals(snapshot))
                {
                    _current = snapshot;
                    var toPublish = snapshot;
                    raiseEvent = () => SnapshotChanged?.Invoke(this, toPublish);
                }
                else
                {
                    raiseEvent = () => { };
                }
            }

            raiseEvent();

            await OptOutAsync().ConfigureAwait(false);
        }

        private async Task OptOutAsync()
        {
            bool shouldOptOut;
            lock (_gate)
            {
                shouldOptOut = _optedIn;
                if (shouldOptOut)
                {
                    _optedIn = false;
                }
            }

            if (!shouldOptOut)
            {
                return;
            }

            _logger.Info("[auth] browser sign-in turned off; restarting cs-ide");

            try
            {
                await _client.RestartAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Warn($"[auth] restart failed: {ex.Message}");
            }
        }

        private async Task ApplyStatusAsync(JObject status, bool reloadAccountData = false)
        {
            bool shouldApply;
            JObject previousStatus;
            Action startAccountLoad;
            bool signedIn;
            bool sync;
            bool shouldSettle;
            Action raiseEvent;

            lock (_gate)
            {
                shouldApply = _capable && _settings.BrowserSignInEnabled;
                previousStatus = _status;
                _status = status;

                if (!shouldApply)
                {
                    return;
                }

                startAccountLoad = PrepareAccountLoad(status, reloadAccountData);
                if (_reauthorizeLoginId != null && !AuthStatusFields.ReauthorizeNeeded(status))
                {
                    _reauthorizeLoginId = null;
                }

                var snapshot = _current.WithActive(true).WithStatus(status);
                raiseEvent = Publish(snapshot);

                signedIn = AuthStatusFields.AccountDataKey(status) != null;
                sync = TakeSignInSync(signedIn);
                shouldSettle = sync || AwaitsResolution(signedIn);
            }

            raiseEvent();

            var outcome = AuthTelemetryEvents.SignInOutcome(previousStatus, status);
            if (outcome.HasValue)
            {
                await _telemetry.SendTelemetryAsync(outcome.Value.Key, outcome.Value.Value).ConfigureAwait(false);
            }

            startAccountLoad();

            if (shouldSettle)
            {
                await SettleAsync(sync).ConfigureAwait(false);
            }
        }

        private Action Publish(AuthSnapshot snapshot)
        {
            var step = AuthFlowSteps.Next(StepFlow(), snapshot);

            if (step == AuthFlowSteps.DONE)
            {
                _flowGeneration++;
                _flow = AuthFlowState.Idle;
                snapshot = snapshot.WithSurface(AuthConstants.Surfaces.MONITOR).WithStep(AuthConstants.Steps.SUMMARY);
            }
            else
            {
                snapshot = snapshot.WithStep(step);
            }

            var context = _flow.Kind == AuthConstants.FlowKinds.SIGNIN
                ? AuthConstants.Contexts.SIGNIN
                : AuthConstants.Contexts.MANUAL;
            snapshot = snapshot.WithContext(context);

            snapshot = snapshot.WithSyncing(IsSyncing());
            snapshot = snapshot.WithSignInBannerHidden(_signInBannerHidden);

            if (_current.Equals(snapshot))
            {
                return () => { };
            }

            _current = snapshot;
            var toPublish = snapshot;
            return () => SnapshotChanged?.Invoke(this, toPublish);
        }
    }
}
