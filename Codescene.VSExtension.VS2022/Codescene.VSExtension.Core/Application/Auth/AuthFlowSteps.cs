// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Consts;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public static class AuthFlowSteps
    {
        public const string DONE = "done";

        public static string Next(AuthFlowState flow, AuthSnapshot snapshot)
        {
            var state = AuthStatusFields.State(snapshot.Status);
            if (flow.Kind == AuthConstants.FlowKinds.IDLE || state != AuthConstants.States.SIGNEDIN)
            {
                return AuthConstants.Steps.SUMMARY;
            }

            var manualStep = ManualStep(flow);
            if (manualStep != null)
            {
                return manualStep;
            }

            if (!flow.Resolved)
            {
                return AuthConstants.Steps.RESOLVING;
            }

            if (NeedsAccountPick(flow, snapshot))
            {
                return AuthConstants.Steps.PICKACCOUNT;
            }

            return NeedsProjectPick(flow, snapshot) ? AuthConstants.Steps.PICKPROJECT : DONE;
        }

        private static string ManualStep(AuthFlowState flow)
        {
            if (flow.Kind == AuthConstants.FlowKinds.MANUALACCOUNT)
            {
                return flow.AccountChosen ? DONE : AuthConstants.Steps.PICKACCOUNT;
            }

            return flow.Kind == AuthConstants.FlowKinds.MANUALPROJECT ? AuthConstants.Steps.PICKPROJECT : null;
        }

        private static bool NeedsAccountPick(AuthFlowState flow, AuthSnapshot snapshot)
        {
            var several = (snapshot.Accounts?.Count ?? 0) > 1;
            return several && snapshot.LinkedProject == null && !flow.AccountChosen;
        }

        private static bool NeedsProjectPick(AuthFlowState flow, AuthSnapshot snapshot)
        {
            var hasProjects = (snapshot.Projects?.Count ?? 0) > 0;
            return snapshot.Repo != null && hasProjects && snapshot.LinkedProject == null && !flow.ProjectSkipped;
        }
    }
}
