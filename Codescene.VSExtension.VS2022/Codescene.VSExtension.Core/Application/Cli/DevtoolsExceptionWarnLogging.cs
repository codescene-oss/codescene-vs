// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Linq;
using Codescene.VSExtension.Core.Exceptions;

namespace Codescene.VSExtension.Core.Application.Cli
{
    internal static class DevtoolsExceptionWarnLogging
    {
        private static readonly Func<DevtoolsException, bool>[] WarnPredicates =
        {
            IsRefactoringCreditsExhausted,
        };

        internal static bool ShouldLogAsWarning(DevtoolsException ex)
        {
            if (ex == null)
            {
                return false;
            }

            return WarnPredicates.Any(p => p(ex));
        }

        private static bool IsRefactoringCreditsExhausted(DevtoolsException ex)
        {
            var message = ex.Message;
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            return message.IndexOf("credits", StringComparison.OrdinalIgnoreCase) >= 0
                && message.IndexOf("refactoring", StringComparison.OrdinalIgnoreCase) >= 0
                && (message.IndexOf("ran out", StringComparison.OrdinalIgnoreCase) >= 0
                    || message.IndexOf("bigger plan", StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
