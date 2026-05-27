// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Text;

namespace Codescene.VSExtension.Core.Application.Security;

internal static class SecretBuffer
{
    public static byte[] FromString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return Array.Empty<byte>();
        }

        return Encoding.UTF8.GetBytes(value);
    }

    public static void Clear(ref byte[] buffer)
    {
        if (buffer == null || buffer.Length == 0)
        {
            buffer = null;
            return;
        }

        Array.Clear(buffer, 0, buffer.Length);
        buffer = null;
    }

    public static void Replace(ref byte[] current, string newValue)
    {
        Clear(ref current);
        current = FromString(newValue);
    }

    public static bool Equals(byte[] left, string right)
    {
        var rightBytes = FromString(right);
        try
        {
            return ConstantTimeEquals(left, rightBytes);
        }
        finally
        {
            if (rightBytes.Length > 0)
            {
                Array.Clear(rightBytes, 0, rightBytes.Length);
            }
        }
    }

    private static bool ConstantTimeEquals(byte[] a, byte[] b)
    {
        if (a == null || a.Length == 0)
        {
            return b == null || b.Length == 0;
        }

        if (b == null || b.Length == 0)
        {
            return false;
        }

        if (a.Length != b.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }

        return diff == 0;
    }
}
