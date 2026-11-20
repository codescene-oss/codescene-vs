// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Consts;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public sealed class AuthFlowState : IEquatable<AuthFlowState>
    {
        public AuthFlowState(string kind, bool resolved, bool accountChosen, bool projectSkipped)
        {
            Kind = kind;
            Resolved = resolved;
            AccountChosen = accountChosen;
            ProjectSkipped = projectSkipped;
        }

        public static AuthFlowState Idle { get; } = new AuthFlowState(AuthConstants.FlowKinds.IDLE, false, false, false);

        public string Kind { get; }

        public bool Resolved { get; }

        public bool AccountChosen { get; }

        public bool ProjectSkipped { get; }

        public AuthFlowState WithKind(string kind)
        {
            return new AuthFlowState(kind, Resolved, AccountChosen, ProjectSkipped);
        }

        public AuthFlowState WithResolved(bool resolved)
        {
            return new AuthFlowState(Kind, resolved, AccountChosen, ProjectSkipped);
        }

        public AuthFlowState WithAccountChosen(bool accountChosen)
        {
            return new AuthFlowState(Kind, Resolved, accountChosen, ProjectSkipped);
        }

        public AuthFlowState WithProjectSkipped(bool projectSkipped)
        {
            return new AuthFlowState(Kind, Resolved, AccountChosen, projectSkipped);
        }

        public bool Equals(AuthFlowState other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Kind == other.Kind && Resolved == other.Resolved && AccountChosen == other.AccountChosen && ProjectSkipped == other.ProjectSkipped;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AuthFlowState);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Kind?.GetHashCode() ?? 0;
                hashCode = (hashCode * 397) ^ Resolved.GetHashCode();
                hashCode = (hashCode * 397) ^ AccountChosen.GetHashCode();
                hashCode = (hashCode * 397) ^ ProjectSkipped.GetHashCode();
                return hashCode;
            }
        }
    }
}
