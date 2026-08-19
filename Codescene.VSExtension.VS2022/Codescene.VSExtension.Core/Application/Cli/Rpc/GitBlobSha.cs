// Copyright (c) CodeScene. All rights reserved.

using System.Security.Cryptography;
using System.Text;

namespace Codescene.VSExtension.Core.Application.Cli.Rpc
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
            var header = Encoding.UTF8.GetBytes("blob " + bytes.Length + "\0");
            using (var sha1 = SHA1.Create())
            {
                sha1.TransformBlock(header, 0, header.Length, header, 0);
                sha1.TransformFinalBlock(bytes, 0, bytes.Length);
                var hash = sha1.Hash;
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
