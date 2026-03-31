// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Linq;
using Codescene.VSExtension.Core.Exceptions;

namespace Codescene.VSExtension.Core.Application.Cli
{
    internal static class CliExceptionWarnLogging
    {
        private static readonly Func<Exception, bool>[] WarnPredicates =
        {
            IsMissingAuthToken,
            IsDevtoolsRefactoringCreditsExhausted,
        };

        internal static bool ShouldLogAsWarning(Exception ex)
        {
            if (ex == null)
            {
                return false;
            }

            return WarnPredicates.Any(p => p(ex));
        }

        internal static string FormatWarningMessage(Exception ex, string operationContext)
        {
            var ctx = (operationContext ?? string.Empty).TrimEnd('.', ' ');

            if (ex is DevtoolsException e)
            {
                var trace = string.IsNullOrEmpty(e.TraceId) ? "n/a" : e.TraceId;
                return $"{ctx}: {e.Message} (status {e.Status}, trace {trace})";
            }

            return $"{ctx}: {ex.Message}";
        }

        private static bool IsMissingAuthToken(Exception ex) => ex is MissingAuthTokenException;

        private static bool IsDevtoolsRefactoringCreditsExhausted(Exception ex) =>
            ex is DevtoolsException d && IsRefactoringCreditsExhausted(d);

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
