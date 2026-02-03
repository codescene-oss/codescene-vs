// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json;
using static Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ICliExecutor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliExecutor : ICliExecutor
    {
        private readonly ILogger _logger;
        private readonly ICliCommandProvider _cliCommandProvider; // TODO: evaulate if this is needed, if  ExecuteCommand(string arguments, string content = null) is replaced with IProcessExecutor
        private readonly IProcessExecutor _executor;
        private readonly ISettingsProvider _settingsProvider;
        private readonly ICacheStorageService _cacheStorageService;
        private readonly Lazy<ITelemetryManager> _telemetryManagerLazy;

        [ImportingConstructor]
        public CliExecutor(
            ILogger logger,
            ICliCommandProvider cliCommandProvider,
            IProcessExecutor executor,
            ISettingsProvider settingsProvider,
            ICacheStorageService cacheStorageService,
            [Import(AllowDefault = true)] Lazy<ITelemetryManager> telemetryManagerLazy = null)
        {
            _logger = logger;
            _cliCommandProvider = cliCommandProvider;
            _executor = executor;
            _settingsProvider = settingsProvider;
            _cacheStorageService = cacheStorageService;
            _telemetryManagerLazy = telemetryManagerLazy;
        }

        private ITelemetryManager GetTelemetryManager()
        {
            try
            {
                return _telemetryManagerLazy?.Value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reviews a file's content by invoking the CLI with the appropriate arguments.
        /// </summary>
        /// <param name="filename">The name of the file being reviewed (used for context, not passed to CLI).</param>
        /// <param name="content">The content (code) of the file to be reviewed.</param>
        /// <returns>A <see cref="CliReviewModel"/> containing the review results, or null if the review fails.</returns>
        public CliReviewModel ReviewContent(string filename, string content)
        {
            var command = _cliCommandProvider.ReviewFileContentCommand;
            var payload = _cliCommandProvider.GetReviewFileContentPayload(filename, content, _cacheStorageService.GetSolutionReviewCacheLocation());

            long elapsedMs = 0;
            var result = ExecuteWithTimingAndLogging<CliReviewModel>(
                $"CLI file review",
                () => _executor.Execute(command, payload),
                $"Review of file {filename} failed",
                out elapsedMs
            );

            if (result != null)
            {
                var loc = PerformanceTelemetryHelper.CalculateLineCount(content);
                var language = PerformanceTelemetryHelper.ExtractLanguage(filename);
                var telemetryData = new PerformanceTelemetryData
                {
                    Type = Titles.REVIEW,
                    ElapsedMs = elapsedMs,
                    FilePath = filename,
                    Loc = loc,
                    Language = language,
                };
                PerformanceTelemetryHelper.SendPerformanceTelemetry(GetTelemetryManager(), _logger, telemetryData);
            }

            return result;
        }

        /// <summary>
        /// Executes a delta review between two versions of a file using their respective scores.
        /// Either <paramref name="oldScore"/> or <paramref name="newScore"/> may be null, but not both.
        /// </summary>
        /// <param name="oldScore">The raw score of the old file version's review.</param>
        /// <param name="newScore">The raw score of the new file version's review.</param>
        /// <param name="filePath">Optional file path for telemetry purposes.</param>
        /// <param name="fileContent">Optional file content for telemetry purposes.</param>
        /// <returns>A <see cref="DeltaResponseModel"/> containing delta results, or null if execution fails or arguments are invalid.</returns>
        public DeltaResponseModel ReviewDelta(string oldScore, string newScore, string filePath = null, string fileContent = null)
        {
            var arguments = _cliCommandProvider.GetReviewDeltaCommand(oldScore, newScore);

            if (string.IsNullOrEmpty(arguments))
            {
                _logger.Warn("Skipping delta review. Arguments were not defined.");
                return null;
            }

            long elapsedMs = 0;
            var result = ExecuteWithTimingAndLogging<DeltaResponseModel>(
                "CLI file delta review",
                () => _executor.Execute(Titles.DELTA, arguments),
                "Delta for file failed.",
                out elapsedMs
            );

            if (result != null && !string.IsNullOrEmpty(filePath))
            {
                var loc = PerformanceTelemetryHelper.CalculateLineCount(fileContent);
                var language = PerformanceTelemetryHelper.ExtractLanguage(filePath);
                var telemetryData = new PerformanceTelemetryData
                {
                    Type = Titles.DELTA,
                    ElapsedMs = elapsedMs,
                    FilePath = filePath,
                    Loc = loc,
                    Language = language,
                };
                PerformanceTelemetryHelper.SendPerformanceTelemetry(GetTelemetryManager(), _logger, telemetryData);
            }

            return result;
        }

        public PreFlightResponseModel Preflight(bool force = true)
        {
            var arguments = _cliCommandProvider.GetPreflightSupportInformationCommand(force: force);
            if (string.IsNullOrEmpty(arguments))
            {
                _logger.Warn("Skipping preflight. Arguments were not defined.");
                return null;
            }

            return ExecuteWithTimingAndLogging<PreFlightResponseModel>(
                "ACE preflight",
                () => _executor.Execute(arguments, null, Timeout.TELEMETRYTIMEOUT),
                "Preflight failed.");
        }

        public RefactorResponseModel PostRefactoring(FnToRefactorModel fnToRefactor, bool skipCache = false, string token = null)
        {
            var effectiveToken = string.IsNullOrEmpty(token) ? _settingsProvider.AuthToken : token;
            if (string.IsNullOrEmpty(effectiveToken))
            {
                throw new MissingAuthTokenException("Authentication token is missing. Please set it in the extension settings.");
            }

            var arguments = _cliCommandProvider.GetRefactorPostCommand(fnToRefactor: fnToRefactor, skipCache: skipCache, token: effectiveToken);
            if (string.IsNullOrEmpty(arguments))
            {
                _logger.Warn("Skipping refactoring. Arguments were not defined.");
                return null;
            }

            long elapsedMs = 0;
            var result = ExecuteWithTimingAndLogging<RefactorResponseModel>(
                "ACE refactoring",
                () => _executor.Execute(arguments),
                "Refactoring failed.",
                out elapsedMs
            );

            if (result != null && fnToRefactor != null)
            {
                var loc = PerformanceTelemetryHelper.CalculateLineCount(fnToRefactor.Body);
                var language = PerformanceTelemetryHelper.ExtractLanguage(null, fnToRefactor);
                var telemetryData = new PerformanceTelemetryData
                {
                    Type = Titles.ACE,
                    ElapsedMs = elapsedMs,
                    Loc = loc,
                    Language = language,
                    FnToRefactor = fnToRefactor,
                };
                SendPerformanceTelemetry(telemetryData);
            }

            return result;
        }

        private void SendPerformanceTelemetry(PerformanceTelemetryData telemetryData)
        {
            var telemetryManager = GetTelemetryManager();
            Task.Run(() =>
            {
                try
                {
                    PerformanceTelemetryHelper.SendPerformanceTelemetry(telemetryManager, _logger, telemetryData);
                }
                catch (Exception e)
                {
                    _logger?.Debug($"Failed to send performance telemetry asynchronously: {e.Message}");
                }
            });
        }

        public IList<FnToRefactorModel> FnsToRefactorFromCodeSmells(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight)
        {
            return ExecuteFnsToRefactor(
                isValid: codeSmells != null && codeSmells.Count > 0,
                skipMessage: "Skipping refactoring functions from code smells. Code smells list was null or empty.",
                getPayload: cachePath => _cliCommandProvider.GetRefactorWithCodeSmellsPayload(fileName, fileContent, cachePath, codeSmells, preflight),
                operationLabel: "ACE refactoring functions from code smells check");
        }

        public IList<FnToRefactorModel> FnsToRefactorFromDelta(string fileName, string fileContent, DeltaResponseModel deltaResult, PreFlightResponseModel preflight)
        {
            return ExecuteFnsToRefactor(
                isValid: deltaResult != null,
                skipMessage: "Skipping refactoring functions from delta. Delta result was null.",
                getPayload: cachePath => _cliCommandProvider.GetRefactorWithDeltaResultPayload(fileName, fileContent, cachePath, deltaResult, preflight),
                operationLabel: "ACE refactoring functions from delta check");
        }

        public string GetDeviceId()
        {
            var arguments = _cliCommandProvider.DeviceIdCommand;
            return ExecuteSimpleCommand(arguments, "Could not get device ID");
        }

        public string GetFileVersion()
        {
            var arguments = _cliCommandProvider.VersionCommand;
            return ExecuteSimpleCommand(arguments, "Could not get CLI version");
        }

        private IList<FnToRefactorModel> ExecuteFnsToRefactor(
    bool isValid,
    string skipMessage,
    Func<string, string> getPayload,
    string operationLabel)
        {
            if (!isValid)
            {
                _logger.Debug(skipMessage);
                return null;
            }

            var cachePath = _cacheStorageService.GetSolutionReviewCacheLocation();
            var payloadContent = getPayload(cachePath);

            return ExecuteFnsToRefactorCommand(payloadContent, operationLabel, operationLabel + " failed.");
        }

        private IList<FnToRefactorModel> ExecuteFnsToRefactorCommand(string payloadContent, string operationLabel, string errorMessage)
        {
            _cacheStorageService.RemoveOldReviewCacheEntries();

            if (string.IsNullOrEmpty(payloadContent))
            {
                _logger.Warn("Skipping refactoring functions check. Payload content was not defined.");
                return null;
            }

            var command = _cliCommandProvider.RefactorCommand;

            return ExecuteWithTimingAndLogging<IList<FnToRefactorModel>>(
                operationLabel,
                () => _executor.Execute(command, payloadContent),
                errorMessage);
        }

        private T ExecuteWithTimingAndLogging<T>(string label, Func<string> execute, string errorMessage)
        {
            long elapsedMs;
            return ExecuteWithTimingAndLogging<T>(label, execute, errorMessage, out elapsedMs);
        }

        private T ExecuteWithTimingAndLogging<T>(string label, Func<string> execute, string errorMessage, out long elapsedMs)
        {
            elapsedMs = 0;
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = execute();
                stopwatch.Stop();
                elapsedMs = stopwatch.ElapsedMilliseconds;

                _logger.Debug($"{Titles.CODESCENE} {label} completed in {elapsedMs} ms.");
                return JsonConvert.DeserializeObject<T>(result);
            }
            catch (DevtoolsException e)
            {
                _logger.Error(errorMessage, e);
                throw e;
            }
            catch (Exception e)
            {
                _logger.Error(errorMessage, e);
                return default;
            }
        }

        private string ExecuteSimpleCommand(string arguments, string errorMessage)
        {
            try
            {
                var result = _executor.Execute(arguments);

                return result?.Trim().TrimEnd('\r', '\n');
            }
            catch (Exception e)
            {
                _logger.Error(errorMessage, e);
                return string.Empty;
            }
        }

    }
}
