// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Models.Cli.Telemetry;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Codescene.VSExtension.Core.Util
{
    public static class TelemetryUtils
    {
        private const string VsCommonSqmRelativePathTemplate = @"SOFTWARE\Wow6432Node\Microsoft\VSCommon\{0}\SQM";
        private const string OptInValueName = "OptIn";
        private static readonly string[] FallbackVsCommonVersionFolders = { "18.0", "17.0" };

        private static bool? _telemetryEnabledOverrideForTests;

        internal static bool? TelemetryEnabledOverrideForTests
        {
            get => _telemetryEnabledOverrideForTests;
            set => _telemetryEnabledOverrideForTests = value;
        }

        public static string GetTelemetryEventJson(string eventName, string deviceId, string version, string editorVersion = null, Dictionary<string, object> additionalEventData = null)
        {
            var telemetryEvent = new TelemetryEvent
            {
                UserId = deviceId,
                EditorType = Constants.Telemetry.SOURCEIDE,
                EditorVersion = editorVersion,
                EventName = $"{Constants.Telemetry.SOURCEIDE}/{eventName}",
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
        /// Visual Studio stores telemetry opt-in under
        /// <c>HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Microsoft\VSCommon\{major}.0\SQM</c>
        /// (for example <c>17.0</c> for VS 2022, <c>18.0</c> for newer releases), DWORD <c>OptIn</c>.
        /// The <paramref name="editorVersion"/> from the running VS instance (for example <c>18.1.1</c>) is mapped to that folder.
        ///
        /// Value meanings:
        /// - 1: User has opted in to telemetry collection (enabled)
        /// - 0: User has opted out of telemetry collection (disabled)
        ///
        /// For more information, see:
        /// https://learn.microsoft.com/en-us/visualstudio/ide/visual-studio-experience-improvement-program?view=vs-2022.
        /// </remarks>
        /// <param name="logger">Optional logger for diagnostics.</param>
        /// <param name="editorVersion">Running Visual Studio version (for example from <c>VSSPROPID_ReleaseVersion</c>).</param>
        /// <returns>True if telemetry is enabled (opted in); otherwise, false.</returns>
        public static bool IsTelemetryEnabled(ILogger logger = null, string editorVersion = null)
        {
            if (_telemetryEnabledOverrideForTests.HasValue)
            {
                return _telemetryEnabledOverrideForTests.Value;
            }

            try
            {
                var versionFolder = GetVsCommonRegistryVersionFolder(editorVersion);
                if (!string.IsNullOrEmpty(versionFolder) &&
                    TryReadTelemetryOptIn(versionFolder, out var enabledForRunningVersion))
                {
                    return enabledForRunningVersion;
                }

                if (!string.IsNullOrEmpty(editorVersion))
                {
                    logger?.Debug($"Telemetry opt-in registry key not found for VS {versionFolder ?? editorVersion}.");
                    return false;
                }

                foreach (var fallbackFolder in FallbackVsCommonVersionFolders)
                {
                    if (TryReadTelemetryOptIn(fallbackFolder, out var enabled))
                    {
                        return enabled;
                    }
                }

                return false;
            }
            catch (Exception e)
            {
                logger?.Debug($"Unable to check if telemetry is enabled: {e.Message}. Defaulting to false.");
                return false;
            }
        }

        internal static string GetVsCommonRegistryVersionFolder(string editorVersion)
        {
            if (string.IsNullOrWhiteSpace(editorVersion))
            {
                return null;
            }

            var parts = editorVersion.Split('.');
            if (parts.Length == 0)
            {
                return null;
            }

            if (!int.TryParse(parts[0], out var major))
            {
                return null;
            }

            if (major < 1)
            {
                return null;
            }

            return $"{major}.0";
        }

        private static bool TryReadTelemetryOptIn(string vsCommonVersionFolder, out bool enabled)
        {
            enabled = false;
            var keyPath = string.Format(VsCommonSqmRelativePathTemplate, vsCommonVersionFolder);
            using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
            {
                if (key == null)
                {
                    return false;
                }

                var optInValue = key.GetValue(OptInValueName);
                enabled = optInValue is int intVal && intVal == 1;
                return true;
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

                TelemetryDataSanitizer.SanitizeJObject(jObject);

                return jObject.ToString(Formatting.None);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
