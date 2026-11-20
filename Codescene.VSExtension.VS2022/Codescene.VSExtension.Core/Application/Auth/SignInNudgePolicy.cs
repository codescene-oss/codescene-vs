// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Consts;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public static class SignInNudgePolicy
    {
        public static bool ShouldNudge(AuthSnapshot snapshot)
        {
            if (!snapshot.Active)
            {
                return false;
            }

            var status = snapshot.Status;
            if (status == null)
            {
                return false;
            }

            var state = AuthStatusFields.State(status);
            if (state != AuthConstants.States.SIGNEDOUT)
            {
                return false;
            }

            if (AuthStatusFields.EnvTokenPresent(status))
            {
                return false;
            }

            var oauthSupported = AuthStatusFields.OAuthSupported(status);
            if (oauthSupported == false)
            {
                return false;
            }

            var errorCode = AuthStatusFields.ErrorCode(status);
            if (errorCode != null && errorCode != "auth/invalid-token")
            {
                return false;
            }

            return true;
        }

        public static bool ShouldShow(AuthSnapshot snapshot, bool shownThisSession, bool dismissed)
        {
            if (!ShouldNudge(snapshot))
            {
                return false;
            }

            if (shownThisSession || dismissed)
            {
                return false;
            }

            return true;
        }
    }
}
