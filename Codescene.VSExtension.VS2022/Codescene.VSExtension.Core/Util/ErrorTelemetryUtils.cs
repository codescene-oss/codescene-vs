using System;
using System.Collections.Generic;
using System.Linq;

namespace Codescene.VSExtension.Core.Util
{
    public static class ErrorTelemetryUtils
    {
        private static int _sentErrorsCount = 0;
        private const int MAXERRORSTOSEND = 5;

        private static readonly string[] NetworkErrorPatterns = new[]
        {
            "java.net.ConnectException",
            "getaddrinfo ENOTFOUND",
            "ECONNREFUSED",
            "Exceptional status code",
            "at babashka.http_client",
            "status 3",
            "status 4",
            "status 5",
            "SocketException",
            "WebException",
            "HttpRequestException",
            "No such host is known",
            "Unable to connect to the remote server",
            "The remote name could not be resolved",
        };

        public static bool ShouldSendError(Exception ex)
        {
            if (_sentErrorsCount >= MAXERRORSTOSEND)
            {
                return false;
            }

            if (IsTelemetryRelatedError(ex))
            {
                return false;
            }

            if (IsNetworkError(ex))
            {
                return false;
            }

            return true;
        }

        private static bool IsNetworkError(Exception ex)
        {
            var message = ex.Message ?? string.Empty;
            var exceptionType = ex.GetType().Name;

            if (NetworkErrorPatterns.Any(pattern =>
                message.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0 ||
                exceptionType.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            if (ex.InnerException != null)
            {
                return IsNetworkError(ex.InnerException);
            }

            return false;
        }

        public static void IncrementErrorCount()
        {
            _sentErrorsCount++;
        }

        public static void ResetErrorCount()
        {
            _sentErrorsCount = 0;
        }

        public static Dictionary<string, object> SerializeException(Exception ex, string context)
        {
            var result = new Dictionary<string, object>
            {
                ["name"] = ex.GetType().Name,
                ["message"] = ex.Message ?? string.Empty,
            };

            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                result["stack"] = ex.StackTrace;
            }

            var extraData = new Dictionary<string, object>
            {
                ["context"] = context ?? string.Empty,
            };

            if (ex.InnerException != null)
            {
                extraData["innerExceptionName"] = ex.InnerException.GetType().Name;
                extraData["innerExceptionMessage"] = ex.InnerException.Message ?? string.Empty;
            }

            result["extraData"] = extraData;

            return result;
        }

        private static bool IsTelemetryRelatedError(Exception ex)
        {
            var message = ex.Message?.ToLowerInvariant() ?? string.Empty;
            return message.Contains("telemetry");
        }
    }
}
