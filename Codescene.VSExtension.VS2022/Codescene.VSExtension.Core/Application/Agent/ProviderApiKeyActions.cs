// Copyright (c) CodeScene. All rights reserved.

using Codescene.VSExtension.Core.Models.Agent;

namespace Codescene.VSExtension.Core.Application.Agent
{
    public static class ProviderApiKeyActions
    {
        public const string ConnectivityNotUsableMessage = "Enable \"Enable Refactoring Agent\" and sign in to CodeScene to test provider connectivity.";

        public static string ConnectivityReport(ConnectivityResult result)
        {
            if (result == null)
            {
                return null;
            }

            if (result.Cancelled)
            {
                return null;
            }

            if (result.Success)
            {
                return "Provider connectivity test successful.";
            }

            return $"Provider connectivity test failed: {result.Error}";
        }
    }
}
