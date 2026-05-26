// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Security.Cryptography;
using System.Text;

namespace Codescene.VSExtension.Core.Application.Security;

internal static class AuthTokenProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Codescene.VSExtension.VS2022.AuthToken.v1");

    public static string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return string.Empty;
        }

        var data = Encoding.UTF8.GetBytes(plaintext);
        var protectedBytes = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(protectedBytes);
    }

    public static string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return string.Empty;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(stored);
            var data = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch (FormatException)
        {
            return stored;
        }
        catch (CryptographicException)
        {
            return stored;
        }
    }
}
