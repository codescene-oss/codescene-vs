using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models.Cli.Telemetry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Util
{
    public static class TelemetryUtils
    {
        public static string GetTelemetryEventJson(string eventName, string deviceId, string version, Dictionary<string, object> additionalEventData = null)
        {
            var telemetryEvent = new TelemetryEvent
            {
                UserId = deviceId,
                EditorType = Constants.Telemetry.SOURCE_IDE,
                EventName = $"{Constants.Telemetry.SOURCE_IDE}/{eventName}",
                ExtensionVersion = version,
            };

            string eventJson = Serialize(telemetryEvent, additionalEventData);

            return eventJson;
        }

        /// <summary>
        /// Checks if the user has opted in to the Visual Studio Customer Experience Improvement Program (VSCEIP) telemetry (enabled by default).
        /// This setting can be changed by the user via <c>Help > Privacy > Privacy Settings...</c> in Visual Studio.
        /// By relying on this official opt-in status, our extension respects the user's choice regarding telemetry.
        /// </summary>
        /// <remarks>
        /// Visual Studio 2022 stores telemetry opt-in status in the registry key:
        /// <c>HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\VSCommon\17.0\SQM</c>
        /// under the DWORD value <c>OptIn</c>.
        /// 
        /// Value meanings:
        /// - 1: User has opted in to telemetry collection (enabled)
        /// - 0: User has opted out of telemetry collection (disabled)
        ///
        /// For more information, see:
        /// https://learn.microsoft.com/en-us/visualstudio/ide/visual-studio-experience-improvement-program?view=vs-2022
        /// </remarks>
        /// <returns>True if telemetry is enabled (opted in); otherwise, false.</returns>
        public static bool IsTelemetryEnabled(ILogger logger = null)
        {
            try
            {
                const string keyPath = @"SOFTWARE\Wow6432Node\Microsoft\VSCommon\17.0\SQM";
                const string valueName = "OptIn";

                var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);

                if (key != null)
                {
                    var optInValue = key.GetValue(valueName);
                    return optInValue is int intVal && intVal == 1;
                }

                return false;
            }
            catch (Exception e)
            {
                logger?.Debug($"Unable to check if telemetry is enabled: {e.Message}. Defaulting to false.");
                return false;
            }
        }

        private static string Serialize(TelemetryEvent telemetryEvent, Dictionary<string, object> additionalProps = null)
        {
            try
            {
                var jObject = JObject.FromObject(telemetryEvent);

                if (additionalProps != null)
                {
                    foreach (var kvp in additionalProps)
                    {
                        jObject[kvp.Key] = JToken.FromObject(kvp.Value);
                    }
                }

                return jObject.ToString(Formatting.None);
            }
            catch
            {
                return "";
            }
        }
    }
}