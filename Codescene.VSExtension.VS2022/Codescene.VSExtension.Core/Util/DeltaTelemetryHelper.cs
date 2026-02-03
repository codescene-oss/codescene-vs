using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models.Cache.Delta;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Util
{
    public static class DeltaTelemetryHelper
    {
        /// <summary>
        /// Sends a telemetry event based on how the delta cache has changed for a specific file.
        /// 
        /// Emits one of the following mutually exclusive events:
        /// - <c>MONITOR_FILE_ADDED</c> when a file appears in the cache but wasn't present before.
        /// - <c>MONITOR_FILE_REMOVED</c> when a file was present before but no longer exists in the current cache.
        /// - <c>MONITOR_FILE_UPDATED</c> when a file exists in both snapshots but has changed.
        /// 
        /// Additional telemetry data is included for the "added" and "updated" events.
        /// </summary>
        /// <param name="previousSnapshot">The state of the delta cache before the current operation.</param>
        /// <param name="currentCache">The current state of the delta cache after the operation.</param>
        /// <param name="entry">The delta cache entry representing the file being evaluated.</param>
        /// <param name="telemetryManager">The telemetry manager responsible for dispatching the event.</param>
        public static void HandleDeltaTelemetryEvent(
            Dictionary<string, DeltaResponseModel> previousSnapshot,
            Dictionary<string, DeltaResponseModel> currentCache,
            DeltaCacheEntry entry,
            ITelemetryManager telemetryManager)
        {
            var eventName = GetTelemetryEventName(previousSnapshot, currentCache, entry.FilePath);
            if (eventName == null) return;

            Task.Run(() =>
            {
                var delta = entry.Delta;
                Dictionary<string, object> additionalData = null;

                var hasAdditionalData = eventName == Constants.Telemetry.MONITOR_FILE_ADDED || eventName == Constants.Telemetry.MONITOR_FILE_UPDATED;
                if (hasAdditionalData)
                    additionalData = new Dictionary<string, object>
                    {
                        { "scoreChange", delta.ScoreChange },
                        { "nIssues", delta.FunctionLevelFindings.Length + delta.FileLevelFindings.Length },
                        { "nRefactorableFunctions", delta.FunctionLevelFindings.TakeWhile(finding => finding.RefactorableFn != null).Count() }
                    };

                telemetryManager?.SendTelemetry(eventName, additionalData);
            });
        }

        private static string GetTelemetryEventName(
            Dictionary<string, DeltaResponseModel> before,
            Dictionary<string, DeltaResponseModel> after,
            string filePath)
        {
            var hadBefore = before.ContainsKey(filePath);
            var hasNow = after.ContainsKey(filePath);

            if (!hadBefore && hasNow) return Constants.Telemetry.MONITOR_FILE_ADDED;
            if (hadBefore && !hasNow) return Constants.Telemetry.MONITOR_FILE_REMOVED;
            if (hadBefore && hasNow) return Constants.Telemetry.MONITOR_FILE_UPDATED;

            return null;
        }
    }
}
