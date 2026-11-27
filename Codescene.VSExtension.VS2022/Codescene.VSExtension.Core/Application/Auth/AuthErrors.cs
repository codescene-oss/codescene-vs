// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Exceptions;

namespace Codescene.VSExtension.Core.Application.Auth
{
    public static class AuthErrors
    {
        public static bool IsMethodNotFound(Exception exception)
        {
            return exception is DevtoolsException devtoolsEx && devtoolsEx.Status == -32601;
        }

        public static string Code(Exception exception)
        {
            return exception is DevtoolsException devtoolsEx ? devtoolsEx.AuthErrorCode : null;
        }
    }
}
