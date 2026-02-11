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
        private readonly ICliServices _cliServices;
        private readonly ISettingsProvider _settingsProvider;
        private readonly Lazy<ITelemetryManager> _telemetryManagerLazy;

        [ImportingConstructor]
        public CliExecutor(
            ILogger logger,
            ICliServices cliServices,
            ISettingsProvider settingsProvider,
            [Import(AllowDefault = true)] Lazy<ITelemetryManager> telemetryManagerLazy = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cliServices = cliServices ?? throw new ArgumentNullException(nameof(cliServices));
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
            _telemetryManagerLazy = telemetryManagerLazy;
        }

        /// <summary>
        /// Reviews a file's content by invoking the CLI with the appropriate arguments.
        /// </summary>
        /// <param name="filename">The name of the file being reviewed (used for context, not passed to CLI).</param>
        /// <param name="content">The content (code) of the file to be reviewed.</param>
        /// <returns>A <see cref="CliReviewModel"/> containing the review results, or null if the review fails.</returns>
        public CliReviewModel ReviewContent(string filename, string content)
        {
            var command = _cliServices.CommandProvider.ReviewFileContentCommand;
            var payload = _cliServices.CommandProvider.GetReviewFileContentPayload(filename, content, _cliServices.CacheStorage.GetSolutionReviewCacheLocation());

            var result = ExecuteWithTimingAndLogging<CliReviewModel>(
                "CLI file review",
                () => _cliServices.ProcessExecutor.Execute(command, payload),
                $"Review of file {filename} failed",
                out var elapsedMs);

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
            var arguments = _cliServices.CommandProvider.GetReviewDeltaCommand(oldScore, newScore);

            if (string.IsNullOrEmpty(arguments))
            {
                _logger.Warn("Skipping delta review. Arguments were not defined.");
                return null;
            }

            var result = ExecuteWithTimingAndLogging<DeltaResponseModel>(
                "CLI file delta review",
                () => _cliServices.ProcessExecutor.Execute(Titles.DELTA, arguments),
                "Delta for file failed.",
                out var elapsedMs);

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
            var arguments = _cliServices.CommandProvider.GetPreflightSupportInformationCommand(force: force);
            if (string.IsNullOrEmpty(arguments))
            {
                _logger.Warn("Skipping preflight. Arguments were not defined.");
                return null;
            }

            return ExecuteWithTimingAndLogging<PreFlightResponseModel>(
                "ACE preflight",
                () => _cliServices.ProcessExecutor.Execute(arguments, null, Timeout.TELEMETRYTIMEOUT),
                "Preflight failed.");
        }

        public RefactorResponseModel PostRefactoring(FnToRefactorModel fnToRefactor, bool skipCache = false, string token = null)
        {
            var effectiveToken = string.IsNullOrEmpty(token) ? _settingsProvider.AuthToken : token;
            if (string.IsNullOrEmpty(effectiveToken))
            {
                throw new MissingAuthTokenException("Authentication token is missing. Please set it in the extension settings.");
            }

            var arguments = _cliServices.CommandProvider.GetRefactorPostCommand(fnToRefactor: fnToRefactor, skipCache: skipCache, token: effectiveToken);
            if (string.IsNullOrEmpty(arguments))
            {
                _logger.Warn("Skipping refactoring. Arguments were not defined.");
                return null;
            }

            var result = ExecuteWithTimingAndLogging<RefactorResponseModel>(
                "ACE refactoring",
                () => _cliServices.ProcessExecutor.Execute(arguments),
                "Refactoring failed.",
                out var elapsedMs);

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

        public IList<FnToRefactorModel> FnsToRefactorFromCodeSmells(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight)
        {
            return ExecuteFnsToRefactor(
                isValid: codeSmells != null && codeSmells.Count > 0,
                skipMessage: "Skipping refactoring functions from code smells. Code smells list was null or empty.",
                getPayload: cachePath => _cliServices.CommandProvider.GetRefactorWithCodeSmellsPayload(fileName, fileContent, cachePath, codeSmells, preflight),
                operationLabel: "ACE refactoring functions from code smells check");
        }

        public IList<FnToRefactorModel> FnsToRefactorFromDelta(string fileName, string fileContent, DeltaResponseModel deltaResult, PreFlightResponseModel preflight)
        {
            return ExecuteFnsToRefactor(
                isValid: deltaResult != null,
                skipMessage: "Skipping refactoring functions from delta. Delta result was null.",
                getPayload: cachePath => _cliServices.CommandProvider.GetRefactorWithDeltaResultPayload(fileName, fileContent, cachePath, deltaResult, preflight),
                operationLabel: "ACE refactoring functions from delta check");
        }

        public string GetDeviceId()
        {
            var arguments = _cliServices.CommandProvider.DeviceIdCommand;
            return ExecuteSimpleCommand(arguments, "Could not get device ID");
        }

        public string GetFileVersion()
        {
            var arguments = _cliServices.CommandProvider.VersionCommand;
            return ExecuteSimpleCommand(arguments, "Could not get CLI version");
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

            var cachePath = _cliServices.CacheStorage.GetSolutionReviewCacheLocation();
            var payloadContent = getPayload(cachePath);

            return ExecuteFnsToRefactorCommand(payloadContent, operationLabel, operationLabel + " failed.");
        }

        private IList<FnToRefactorModel> ExecuteFnsToRefactorCommand(string payloadContent, string operationLabel, string errorMessage)
        {
            _cliServices.CacheStorage.RemoveOldReviewCacheEntries();

            if (string.IsNullOrEmpty(payloadContent))
            {
                _logger.Warn("Skipping refactoring functions check. Payload content was not defined.");
                return null;
            }

            var command = _cliServices.CommandProvider.RefactorCommand;

            return ExecuteWithTimingAndLogging<IList<FnToRefactorModel>>(
                operationLabel,
                () => _cliServices.ProcessExecutor.Execute(command, payloadContent),
                errorMessage);
        }

        private T ExecuteWithTimingAndLogging<T>(string label, Func<string> execute, string errorMessage)
        {
            return ExecuteWithTimingAndLogging<T>(label, execute, errorMessage, out _);
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
                throw;
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
                var result = _cliServices.ProcessExecutor.Execute(arguments);

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
