// Copyright (c) CodeScene. All rights reserved.

using System;

namespace Codescene.VSExtension.Core.Exceptions
{
    public class MissingAuthTokenException : Exception
    {
        public MissingAuthTokenException(string message)
            : base(message)
        {
        }
    }
}
