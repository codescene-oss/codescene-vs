// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public sealed partial class AuthService
    {
        private string _loadedKey;
        private Task _accountLoad = Task.CompletedTask;
        private int _accountLoadGeneration;
        private bool _accountLoading;
        private bool _syncOnSignIn;
        private bool _syncSupported = true;
        private int _flowGeneration;
        private int? _settlingGeneration;
        private int _repoGeneration;
        private bool _repoSyncing;

        public async Task SetRepoAsync(AuthRepo repo)
        {
            if (Equals(repo, _current.Repo))
            {
                return;
            }

            int generation;
            bool shouldSetRepoSyncing;
            Action raiseEvent;
            lock (_gate)
            {
                _repoGeneration++;
                generation = _repoGeneration;
                shouldSetRepoSyncing = _loadedKey != null;
                _repoSyncing = shouldSetRepoSyncing;

                var snapshot = _current.WithRepo(repo)
                    .WithLinkedProject(null)
                    .WithProjectSource(null)
                    .WithProjectReason(null)
                    .WithProjectCandidates(new JArray());

                raiseEvent = Publish(snapshot);
            }

            raiseEvent();

            if (_loadedKey == null)
            {
                return;
            }

            try
            {
                await LookupRepoAsync(repo).ConfigureAwait(false);
            }
            finally
            {
                Action raiseEventFinally;
                lock (_gate)
                {
                    if (generation == _repoGeneration)
                    {
                        _repoSyncing = false;
                        raiseEventFinally = Publish(_current);
                    }
                    else
                    {
                        raiseEventFinally = () => { };
                    }
                }

                raiseEventFinally();
            }
        }

        private async Task LookupRepoAsync(AuthRepo repo)
        {
            if (await SyncAccountAsync().ConfigureAwait(false))
            {
                await AccountDataSettledAsync().ConfigureAwait(false);
                return;
            }

            JObject link = null;
            if (repo != null)
            {
                try
                {
                    link = await _authClient.ProjectLinkGetAsync(repo.Path).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    var code = AuthErrors.Code(ex) ?? "unknown";
                    _logger.Warn($"[auth] account data request failed code={code}");
                }
            }

            PublishLinkFor(repo, link);
        }

        private void PublishLinkFor(AuthRepo repo, JObject link)
        {
            Action raiseEvent;
            lock (_gate)
            {
                if (!IsCurrentRepo(repo))
                {
                    return;
                }

                raiseEvent = Publish(WithLink(_current, link));
            }

            raiseEvent();
        }

        private AuthSnapshot WithLink(AuthSnapshot snapshot, JObject link)
        {
            var (linkedProject, projectSource, projectReason, projectCandidates) = AuthAccountData.LinkFields(link);
            return snapshot
                .WithLinkedProject(linkedProject)
                .WithProjectSource(projectSource)
                .WithProjectReason(projectReason)
                .WithProjectCandidates(projectCandidates);
        }

        private bool IsCurrentRepo(AuthRepo repo)
        {
            return Equals(repo, _current.Repo);
        }

        private Action PrepareAccountLoad(JObject status, bool reload)
        {
            var key = AuthStatusFields.AccountDataKey(status);
            if (!reload && key == _loadedKey)
            {
                return () => { };
            }

            _loadedKey = key;
            _accountLoading = key != null;
            _accountLoadGeneration++;
            var generation = _accountLoadGeneration;

            return () =>
            {
                _accountLoad = FetchAccountDataAsync(key, generation);
            };
        }

        private async Task FetchAccountDataAsync(string key, int generation)
        {
            AuthRepo repo;
            lock (_gate)
            {
                repo = _current.Repo;
            }

            JArray accounts = null;
            JArray projects = null;
            JObject link = null;

            if (key != null)
            {
                (accounts, projects, link) = await AuthAccountData.FetchAsync(_authClient, repo, _logger).ConfigureAwait(false);
            }

            Action raiseEvent;
            lock (_gate)
            {
                if (generation != _accountLoadGeneration)
                {
                    return;
                }

                _accountLoading = false;

                var snapshot = _current.WithAccounts(accounts).WithProjects(projects);

                if (IsCurrentRepo(repo))
                {
                    snapshot = WithLink(snapshot, link);
                }

                raiseEvent = Publish(snapshot);
            }

            raiseEvent();
        }

        private void ForgetAccountData()
        {
            _loadedKey = null;
            _accountLoadGeneration++;
            _accountLoading = false;
        }

        private async Task<bool> SyncAccountAsync()
        {
            var repo = SyncableRepo();
            if (repo == null)
            {
                return false;
            }

            try
            {
                var status = await _authClient.AuthSyncAccountAsync(repo.Path).ConfigureAwait(false);
                var previousKey = AuthStatusFields.AccountDataKey(_status);
                var nextKey = AuthStatusFields.AccountDataKey(status);
                var switched = previousKey != nextKey;

                await ApplyStatusAsync(status).ConfigureAwait(false);
                return switched;
            }
            catch (Exception ex)
            {
                if (AuthErrors.IsMethodNotFound(ex))
                {
                    lock (_gate)
                    {
                        _syncSupported = false;
                    }
                }
                else
                {
                    var code = AuthErrors.Code(ex) ?? "unknown";
                    _logger.Warn($"[auth] account sync failed code={code}");
                }

                return false;
            }
        }

        private AuthRepo SyncableRepo()
        {
            lock (_gate)
            {
                return _capable && _settings.BrowserSignInEnabled && _syncSupported ? _current.Repo : null;
            }
        }

        private async Task SettleAsync(bool sync)
        {
            int flowGen;
            lock (_gate)
            {
                flowGen = _flowGeneration;
                _settlingGeneration = flowGen;
            }

            if (sync)
            {
                await SyncAccountAsync().ConfigureAwait(false);
            }

            await AccountDataSettledAsync().ConfigureAwait(false);

            Action raiseEvent;
            lock (_gate)
            {
                if (flowGen != _flowGeneration)
                {
                    return;
                }

                _settlingGeneration = null;

                if (_flow.Kind != AuthConstants.FlowKinds.SIGNIN)
                {
                    return;
                }

                _flow = _flow.WithResolved(true);
                raiseEvent = Publish(_current);
            }

            raiseEvent();
        }

        private async Task AccountDataSettledAsync()
        {
            Task pending;
            do
            {
                lock (_gate)
                {
                    pending = _accountLoad;
                }

                await pending.ConfigureAwait(false);
            }
            while (!ReferenceEquals(pending, _accountLoad));
        }

        private bool TakeSignInSync(bool signedIn)
        {
            var sync = _syncOnSignIn && signedIn;
            if (sync)
            {
                _syncOnSignIn = false;
            }

            return sync;
        }

        private bool AwaitsResolution(bool signedIn)
        {
            var unresolved = _flow.Kind == AuthConstants.FlowKinds.SIGNIN && !_flow.Resolved;
            return signedIn && unresolved && _settlingGeneration != _flowGeneration;
        }

        private bool IsSyncing()
        {
            var resolvingSignIn = _flow.Kind == AuthConstants.FlowKinds.SIGNIN && !_flow.Resolved;
            return _repoSyncing || _accountLoading || resolvingSignIn;
        }

        private AuthFlowState StepFlow()
        {
            var lookingUpRepo = _repoSyncing && _flow.Kind == AuthConstants.FlowKinds.SIGNIN;
            return lookingUpRepo ? _flow.WithResolved(false) : _flow;
        }
    }
}
