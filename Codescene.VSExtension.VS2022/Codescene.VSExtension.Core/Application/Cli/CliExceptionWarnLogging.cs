// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Linq;
using Codescene.VSExtension.Core.Exceptions;

namespace Codescene.VSExtension.Core.Application.Cli
{
    internal static class CliExceptionWarnLogging
    {
        private static readonly Func<Exception, bool>[] WarnPredicates = { };

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
    }
}
