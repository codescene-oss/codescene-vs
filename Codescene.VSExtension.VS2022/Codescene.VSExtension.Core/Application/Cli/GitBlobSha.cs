// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Security.Cryptography;
using System.Text;

namespace Codescene.VSExtension.Core.Application.Cli
{
    public static class GitBlobSha
    {
        public static string FromUtf8(string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            return FromBytes(bytes);
        }

        public static string FromBytes(byte[] bytes)
        {
            bytes = bytes ?? new byte[0];
            var header = Encoding.ASCII.GetBytes("blob " + bytes.Length + "\0");
            var payload = new byte[header.Length + bytes.Length];
            Buffer.BlockCopy(header, 0, payload, 0, header.Length);
            Buffer.BlockCopy(bytes, 0, payload, header.Length, bytes.Length);
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(payload);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var value in hash)
                {
                    builder.Append(value.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
