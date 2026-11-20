// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Application.Auth
{
    public static class AuthConfiguration
    {
        public static string ServerUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        public static string AccessToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }
    }
}
