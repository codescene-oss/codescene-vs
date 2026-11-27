// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Security.Cryptography;
using System.Text;

namespace Codescene.VSExtension.Core.Application.Security;

internal static class ProviderKeyProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Codescene.VSExtension.VS2022.ProviderKey.v1");

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

    public static bool TryUnprotect(string stored, out string plaintext)
    {
        plaintext = null;

        if (string.IsNullOrEmpty(stored))
        {
            plaintext = string.Empty;
            return true;
        }

        try
        {
            var protectedBytes = Convert.FromBase64String(stored);
            var data = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
            plaintext = Encoding.UTF8.GetString(data);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
