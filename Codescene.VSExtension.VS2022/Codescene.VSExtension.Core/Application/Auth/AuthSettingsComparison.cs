// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Application.Security;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public static class AuthSettingsComparison
    {
        public static (bool PatChanged, bool AuthSettingsChanged) Detect(
            bool prevBrowserSignIn,
            bool currBrowserSignIn,
            string prevServerUrl,
            string currServerUrl,
            byte[] prevPat,
            string currPat)
        {
            var normalizedPrevServerUrl = AuthConfiguration.ServerUrl(prevServerUrl);
            var normalizedCurrServerUrl = AuthConfiguration.ServerUrl(currServerUrl);

            var patChanged = !SecretBuffer.Equals(prevPat, currPat);

            var browserSignInChanged = prevBrowserSignIn != currBrowserSignIn;
            var serverUrlChanged = normalizedPrevServerUrl != normalizedCurrServerUrl;

            var authSettingsChanged = browserSignInChanged || serverUrlChanged || patChanged;

            return (patChanged, authSettingsChanged);
        }
    }
}
