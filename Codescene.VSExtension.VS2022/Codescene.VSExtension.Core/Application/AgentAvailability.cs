// Copyright (c) CodeScene. All rights reserved.

using System;
using System.ComponentModel.Composition;
using Codescene.VSExtension.Core.Application.Auth;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Auth;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.WebComponent.Data;

namespace Codescene.VSExtension.Core.Application
{
    [Export(typeof(IAgentAvailability))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class AgentAvailability : IAgentAvailability
    {
        private readonly ISettingsProvider _settingsProvider;
        private readonly IAuthService _authService;

        private (bool Enabled, bool Authenticated, bool Usable) _previous;

        [ImportingConstructor]
        public AgentAvailability(ISettingsProvider settingsProvider, IAuthService authService)
        {
            _settingsProvider = settingsProvider;
            _authService = authService;

            _previous = CurrentState();

            _authService.SnapshotChanged += OnAuthSnapshotChanged;
        }

        public event EventHandler Changed;

        public bool Enabled => _settingsProvider.EnableRefactoringAgent;

        public bool Authenticated
        {
            get
            {
                var patToken = _settingsProvider.AuthToken;
                if (!string.IsNullOrWhiteSpace(patToken))
                {
                    return true;
                }

                var snapshot = _authService.Current;
                if (!snapshot.Active)
                {
                    return false;
                }

                var state = AuthStatusFields.State(snapshot.Status);
                return state == AuthConstants.States.SIGNEDIN;
            }
        }

        public bool Usable => Enabled && Authenticated;

        public AutoRefactorConfig AutoRefactor()
        {
            return new AutoRefactorConfig
            {
                Visible = Usable,
                Disabled = !Authenticated,
                Activated = true,
                AceStatus = null,
            };
        }

        public void NotifySettingsChanged()
        {
            RaiseChangedIfNeeded();
        }

        private void OnAuthSnapshotChanged(object sender, AuthSnapshot snapshot)
        {
            RaiseChangedIfNeeded();
        }

        private void RaiseChangedIfNeeded()
        {
            var current = CurrentState();

            if (current != _previous)
            {
                _previous = current;

                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        private (bool Enabled, bool Authenticated, bool Usable) CurrentState()
        {
            return (Enabled, Authenticated, Usable);
        }
    }
}
