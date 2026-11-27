// Copyright (c) CodeScene. All rights reserved.

using System;
using Codescene.VSExtension.Core.Exceptions;

namespace Codescene.VSExtension.Core.Application.Refactoring
{
    internal static class RefactoringFailureReason
    {
        public const string TIMEOUT = "timeout";
        public const string AUTH = "auth";
        public const string AGENTERROR = "agent-error";

        public static bool IsCancellation(Exception ex)
        {
            return ex is OperationCanceledException
                || (ex is AgentFailure failure && failure.Kind == AgentFailureKind.Cancelled);
        }

        public static string For(Exception ex)
        {
            if (ex is AgentFailure failure)
            {
                switch (failure.Kind)
                {
                    case AgentFailureKind.Auth:
                        return AUTH;
                    case AgentFailureKind.Timeout:
                        return TIMEOUT;
                }
            }

            if (ex is TimeoutException)
            {
                return TIMEOUT;
            }

            return AGENTERROR;
        }
    }
}
