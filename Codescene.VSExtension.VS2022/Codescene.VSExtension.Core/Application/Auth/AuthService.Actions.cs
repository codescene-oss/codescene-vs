// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public sealed partial class AuthService
    {
        private string _reauthorizeLoginId;
        private bool _signInBannerHidden;

        public async Task SignInAsync()
        {
            if (!IsEnabled())
            {
                return;
            }

            int epoch;
            lock (_gate)
            {
                _syncOnSignIn = true;
                _loginEpoch++;
                epoch = _loginEpoch;
            }

            StartFlow(AuthConstants.FlowKinds.SIGNIN);
            _scheduler.Schedule(() => _host.RevealMonitorAsync());

            await _telemetry.SendTelemetryAsync(Constants.Telemetry.AUTHSIGNINSTARTED, null).ConfigureAwait(false);

            try
            {
                var login = await _authClient.AuthLoginAsync().ConfigureAwait(false);

                bool shouldOpen;
                string loginId;
                string authorizeUrl;
                lock (_gate)
                {
                    shouldOpen = epoch == _loginEpoch;
                    loginId = login?["login-id"]?.Value<string>();
                    authorizeUrl = login?["authorize-url"]?.Value<string>();
                }

                if (!shouldOpen)
                {
                    await CancelLoginAsync(loginId).ConfigureAwait(false);
                    return;
                }

                if (AuthUrlPolicy.IsOpenable(authorizeUrl))
                {
                    await _host.OpenExternalAsync(authorizeUrl).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                var code = AuthErrors.Code(ex);
                var telemetry = AuthTelemetryEvents.SignInFailed(code);
                await _telemetry.SendTelemetryAsync(telemetry.Key, telemetry.Value).ConfigureAwait(false);
            }
        }

        public async Task ReauthorizeAsync()
        {
            bool enabled;
            bool needed;
            lock (_gate)
            {
                enabled = _capable && _settings.BrowserSignInEnabled;
                needed = _status != null && AuthStatusFields.ReauthorizeNeeded(_status);
            }

            if (!enabled || !needed)
            {
                return;
            }

            await _telemetry.SendTelemetryAsync(Constants.Telemetry.AUTHREAUTHORIZESTARTED, null).ConfigureAwait(false);

            try
            {
                var login = await _authClient.AuthReauthorizeAsync().ConfigureAwait(false);

                string loginId;
                string authorizeUrl;
                lock (_gate)
                {
                    loginId = login?["login-id"]?.Value<string>();
                    authorizeUrl = login?["authorize-url"]?.Value<string>();
                    _reauthorizeLoginId = loginId;
                }

                if (AuthUrlPolicy.IsOpenable(authorizeUrl))
                {
                    await _host.OpenExternalAsync(authorizeUrl).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                var code = AuthErrors.Code(ex);
                var telemetry = AuthTelemetryEvents.ReauthorizeFailed(code);
                await _telemetry.SendTelemetryAsync(telemetry.Key, telemetry.Value).ConfigureAwait(false);
            }
        }

        public Task OpenBrowserAgainAsync() => OpenStatusUrlAsync(AuthStatusFields.AuthorizeUrl);

        public void OpenAuth(string screen)
        {
            if (!IsEnabled())
            {
                return;
            }

            bool isSignIn;
            lock (_gate)
            {
                isSignIn = _flow.Kind == AuthConstants.FlowKinds.SIGNIN;
            }

            if (isSignIn)
            {
                Action raiseEvent;
                lock (_gate)
                {
                    var snapshot = _current.WithSurface(AuthConstants.Surfaces.AUTH);
                    raiseEvent = Publish(snapshot);
                }

                raiseEvent();
            }
            else
            {
                var kind = screen == "account" ? AuthConstants.FlowKinds.MANUALACCOUNT :
                          screen == "project" ? AuthConstants.FlowKinds.MANUALPROJECT :
                          AuthConstants.FlowKinds.IDLE;

                StartFlow(kind);
            }

            _scheduler.Schedule(() => _host.RevealMonitorAsync());
        }

        public async Task CloseAuthAsync()
        {
            if (!IsEnabled())
            {
                return;
            }

            string loginId;
            lock (_gate)
            {
                _loginEpoch++;
                _flowGeneration++;
                _flow = AuthFlowState.Idle;
                loginId = _status != null ? AuthStatusFields.LoginId(_status) : null;
                if (string.IsNullOrEmpty(loginId))
                {
                    loginId = _reauthorizeLoginId;
                }
            }

            Action raiseEvent;
            lock (_gate)
            {
                var snapshot = _current.WithSurface(AuthConstants.Surfaces.MONITOR);
                raiseEvent = Publish(snapshot);
            }

            raiseEvent();

            if (!string.IsNullOrEmpty(loginId))
            {
                await CancelLoginAsync(loginId).ConfigureAwait(false);
            }
        }

        public void SkipProject()
        {
            bool isSignIn;
            lock (_gate)
            {
                isSignIn = _flow.Kind == AuthConstants.FlowKinds.SIGNIN;
            }

            if (!isSignIn)
            {
                return;
            }

            Action raiseEvent;
            lock (_gate)
            {
                _flow = _flow.WithProjectSkipped(true);
                raiseEvent = Publish(_current);
            }

            raiseEvent();
        }

        public void HideSignInBanner()
        {
            if (!IsEnabled())
            {
                return;
            }

            Action raiseEvent;
            lock (_gate)
            {
                _signInBannerHidden = true;
                raiseEvent = Publish(_current);
            }

            raiseEvent();
        }

        public async Task SignOutAsync()
        {
            if (!IsEnabled())
            {
                return;
            }

            await _telemetry.SendTelemetryAsync(Constants.Telemetry.AUTHSIGNOUT, null).ConfigureAwait(false);

            var status = await _authClient.AuthLogoutAsync().ConfigureAwait(false);
            await ApplyStatusAsync(status).ConfigureAwait(false);
        }

        public async Task RetryAsync()
        {
            if (!IsEnabled())
            {
                return;
            }

            var status = await _authClient.AuthStatusAsync().ConfigureAwait(false);
            await ApplyStatusAsync(status, true).ConfigureAwait(false);
        }

        public async Task SelectAccountAsync(JToken accountId)
        {
            if (!IsEnabled())
            {
                return;
            }

            int flowGen;
            int epoch;
            lock (_gate)
            {
                flowGen = _flowGeneration;
                epoch = _loginEpoch;
            }

            var status = await _authClient.AuthSelectAccountAsync(accountId).ConfigureAwait(false);

            lock (_gate)
            {
                if (flowGen == _flowGeneration && _flow.Kind != AuthConstants.FlowKinds.IDLE)
                {
                    var resolved = _flow.Kind == AuthConstants.FlowKinds.SIGNIN ? false : _flow.Resolved;
                    _flow = _flow.WithAccountChosen(true).WithResolved(resolved);
                }
            }

            await ApplyStatusAsync(status).ConfigureAwait(false);

            var loginId = AuthStatusFields.LoginId(status);
            var authorizeUrl = AuthStatusFields.AuthorizeUrl(status);

            if (string.IsNullOrEmpty(loginId))
            {
                return;
            }

            bool shouldOpen;
            lock (_gate)
            {
                shouldOpen = epoch == _loginEpoch;
            }

            if (!shouldOpen)
            {
                await CancelLoginAsync(loginId).ConfigureAwait(false);
                return;
            }

            await _telemetry.SendTelemetryAsync(Constants.Telemetry.AUTHSIGNINSTARTED, null).ConfigureAwait(false);

            if (AuthUrlPolicy.IsOpenable(authorizeUrl))
            {
                await _host.OpenExternalAsync(authorizeUrl).ConfigureAwait(false);
            }
        }

        public async Task LinkProjectAsync(JToken projectId)
        {
            AuthRepo repo;
            int flowGen;
            lock (_gate)
            {
                repo = _current.Repo;
                flowGen = _flowGeneration;
            }

            if (!IsEnabled() || repo == null)
            {
                return;
            }

            var link = await SetProjectLinkAsync(repo, projectId).ConfigureAwait(false);
            PublishLinkFor(repo, link);
            EndManualProjectFlow(flowGen);
        }

        public async Task SetAccessTokenAsync(string token)
        {
            if (!IsEnabled())
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(token))
            {
                lock (_gate)
                {
                    _syncOnSignIn = true;
                }

                StartFlow(AuthConstants.FlowKinds.SIGNIN);
            }

            await _settings.SetAccessTokenAsync(token).ConfigureAwait(false);
            await _telemetry.SendTelemetryAsync(Constants.Telemetry.AUTHPATSET, null).ConfigureAwait(false);
        }

        public async Task RemoveAccessTokenAsync()
        {
            if (!IsEnabled())
            {
                return;
            }

            await _settings.SetAccessTokenAsync(string.Empty).ConfigureAwait(false);
            await _telemetry.SendTelemetryAsync(Constants.Telemetry.AUTHPATREMOVED, null).ConfigureAwait(false);
        }

        public Task OpenPatPageAsync() => OpenStatusUrlAsync(AuthStatusFields.PatUrl);

        public async Task OpenServerSettingAsync()
        {
            if (!IsEnabled())
            {
                return;
            }

            await _host.OpenServerSettingAsync().ConfigureAwait(false);
        }

        private bool IsEnabled()
        {
            lock (_gate)
            {
                return _capable && _settings.BrowserSignInEnabled;
            }
        }

        private async Task<JObject> SetProjectLinkAsync(AuthRepo repo, JToken projectId)
        {
            if (projectId == null || projectId.Type == JTokenType.Null)
            {
                await _authClient.ProjectLinkSetAsync(repo.Path, null).ConfigureAwait(false);
                return await _authClient.ProjectLinkGetAsync(repo.Path).ConfigureAwait(false);
            }

            var link = await _authClient.ProjectLinkSetAsync(repo.Path, projectId).ConfigureAwait(false);
            if (link != null)
            {
                link["source"] = "link";
            }

            return link;
        }

        private void EndManualProjectFlow(int flowGen)
        {
            Action raiseEvent;
            lock (_gate)
            {
                if (flowGen != _flowGeneration || _flow.Kind != AuthConstants.FlowKinds.MANUALPROJECT)
                {
                    return;
                }

                _flowGeneration++;
                _flow = AuthFlowState.Idle;
                raiseEvent = Publish(_current.WithSurface(AuthConstants.Surfaces.MONITOR));
            }

            raiseEvent();
        }

        private async Task OpenStatusUrlAsync(Func<JObject, string> read)
        {
            if (!IsEnabled())
            {
                return;
            }

            string url;
            lock (_gate)
            {
                url = read(_status);
            }

            if (AuthUrlPolicy.IsOpenable(url))
            {
                await _host.OpenExternalAsync(url).ConfigureAwait(false);
            }
        }

        private void StartFlow(string kind)
        {
            Action raiseEvent;
            lock (_gate)
            {
                _flowGeneration++;
                _flow = AuthFlowState.Idle.WithKind(kind);
                raiseEvent = Publish(_current.WithSurface(AuthConstants.Surfaces.AUTH));
            }

            raiseEvent();
        }

        private async Task CancelLoginAsync(string loginId)
        {
            if (string.IsNullOrEmpty(loginId))
            {
                return;
            }

            try
            {
                var status = await _authClient.AuthCancelLoginAsync(loginId).ConfigureAwait(false);

                lock (_gate)
                {
                    if (!string.IsNullOrEmpty(_reauthorizeLoginId) && _reauthorizeLoginId == loginId)
                    {
                        _reauthorizeLoginId = null;
                    }
                }

                await ApplyStatusAsync(status).ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }
}
