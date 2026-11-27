// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public sealed class AuthRepo : IEquatable<AuthRepo>
    {
        public AuthRepo(string path, string name)
        {
            Path = path;
            Name = name;
        }

        public string Path { get; }

        public string Name { get; }

        public bool Equals(AuthRepo other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Path == other.Path && Name == other.Name;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as AuthRepo);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Path?.GetHashCode() ?? 0) * 397) ^ (Name?.GetHashCode() ?? 0);
            }
        }
    }
}
