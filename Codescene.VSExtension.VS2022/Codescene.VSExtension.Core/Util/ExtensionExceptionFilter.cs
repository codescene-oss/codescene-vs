// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Diagnostics;

namespace Codescene.VSExtension.Core.Util
{
    public static class ExtensionExceptionFilter
    {
        private const string ExtensionAssemblyPrefix = "Codescene.VSExtension";

        public static bool IsFromExtension(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (ExceptionOriginatesFromExtension(current))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ExceptionOriginatesFromExtension(Exception exception)
        {
            if (IsFromExtensionAssembly(exception.TargetSite?.DeclaringType))
            {
                return true;
            }

            return StackTraceContainsExtensionFrame(exception);
        }

        private static bool StackTraceContainsExtensionFrame(Exception exception)
        {
            var stackTrace = new StackTrace(exception, false);
            for (var i = 0; i < stackTrace.FrameCount; i++)
            {
                if (IsFromExtensionAssembly(stackTrace.GetFrame(i)?.GetMethod()?.DeclaringType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFromExtensionAssembly(Type type)
        {
            if (type == null)
            {
                return false;
            }

            var assemblyName = type.Assembly.GetName().Name;
            return assemblyName != null
                && assemblyName.StartsWith(ExtensionAssemblyPrefix, StringComparison.Ordinal);
        }
    }
}
