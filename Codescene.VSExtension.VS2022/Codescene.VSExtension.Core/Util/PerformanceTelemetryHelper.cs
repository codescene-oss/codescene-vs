using System;
using System.Collections.Generic;
using System.IO;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Refactor;

namespace Codescene.VSExtension.Core.Util
{
    public static class PerformanceTelemetryHelper
    {
        /// <summary>
        /// Sends performance telemetry for CLI analysis operations (review, delta, or ace).
        /// </summary>
        /// <param name="telemetryManager">The telemetry manager to send the event.</param>
        /// <param name="logger">Logger for error handling.</param>
        /// <param name="data">The performance telemetry data.</param>
        public static void SendPerformanceTelemetry(
            ITelemetryManager telemetryManager,
            ILogger logger,
            PerformanceTelemetryData data)
        {
            if (telemetryManager == null || data == null) return;

            try
            {
                var additionalData = new Dictionary<string, object>
                {
                    { "type", data.Type },
                    { "elapsedMs", data.ElapsedMs },
                    { "language", language },
                    { "editor-type", Constants.Telemetry.SOURCEIDE },
                    { "loc", loc }
                };

                telemetryManager.SendTelemetry(Constants.Telemetry.ANALYSISPERFORMANCE, additionalData);
            }
            catch (Exception e)
            {
                logger?.Debug($"Failed to send performance telemetry: {e.Message}");
            }
        }

        /// <summary>
        /// Calculates the line count from content string.
        /// </summary>
        /// <param name="content">The content to count lines in.</param>
        /// <returns>The number of lines in the content, or 0 if content is null or empty.</returns>
        public static int CalculateLineCount(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return 0;
            }

            return content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).Length;
        }

        /// <summary>
        /// Extracts the language from a file path or FnToRefactorModel.
        /// </summary>
        /// <param name="filePathOrName">The file path or name to extract language from.</param>
        /// <param name="fnToRefactor">Optional FnToRefactorModel to extract language from.</param>
        /// <returns>The language/file type, or empty string if not found.</returns>
        public static string ExtractLanguage(string filePathOrName, FnToRefactorModel fnToRefactor = null)
        {
            if (fnToRefactor != null)
            {
                return fnToRefactor.FileType ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(filePathOrName))
            {
                var extension = Path.GetExtension(filePathOrName);
                return string.IsNullOrEmpty(extension) ? string.Empty : extension.TrimStart('.').ToLowerInvariant();
            }

            return string.Empty;
        }
    }
}
