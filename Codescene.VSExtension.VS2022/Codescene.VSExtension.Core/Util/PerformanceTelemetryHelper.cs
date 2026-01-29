using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using System;
using System.Collections.Generic;
using System.IO;

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
                var language = ExtractLanguage(data);
                var loc = CalculateLineCount(data);

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

        private static string ExtractLanguage(PerformanceTelemetryData data)
        {
            if (data.FnToRefactor != null)
            {
                return data.FnToRefactor.FileType ?? "";
            }

            if (!string.IsNullOrEmpty(data.FilePathOrName))
            {
                var extension = Path.GetExtension(data.FilePathOrName);
                return string.IsNullOrEmpty(extension) ? "" : extension.TrimStart('.').ToLowerInvariant();
            }

            return "";
        }

        private static int CalculateLineCount(PerformanceTelemetryData data)
        {
            string content = null;

            if (data.FnToRefactor != null)
            {
                content = data.FnToRefactor.Body;
            }
            else if (!string.IsNullOrEmpty(data.FileContent))
            {
                content = data.FileContent;
            }

            if (string.IsNullOrEmpty(content))
                return 0;

            return content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length;
        }
    }

    /// <summary>
    /// Data container for performance telemetry to reduce primitive obsession.
    /// </summary>
    public class PerformanceTelemetryData
    {
        public string Type { get; set; }
        public long ElapsedMs { get; set; }
        public string FilePathOrName { get; set; }
        public string FileContent { get; set; }
        public FnToRefactorModel FnToRefactor { get; set; }
    }
}

