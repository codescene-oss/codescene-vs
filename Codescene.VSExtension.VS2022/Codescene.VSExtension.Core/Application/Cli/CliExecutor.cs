// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
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
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _inFlightReviewCancellation = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly SemaphoreSlim _deltaChannel = new SemaphoreSlim(1, 1);

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
        /// <param name="isBaseline">True when reviewing baseline (committed) content for delta; used to avoid cancelling in-flight current-content reviews.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="CliReviewModel"/> containing the review results, or null if the review fails.</returns>
        public async Task<CliReviewModel> ReviewContentAsync(string filename, string content, bool isBaseline = false, CancellationToken cancellationToken = default)
        {
            var key = GetReviewCancellationKey(filename, isBaseline);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var oldCts = _inFlightReviewCancellation.AddOrUpdate(key, cts, (_, existing) =>
            {
                try
                {
                    existing.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // ignored
                }

                existing.Dispose();
                return cts;
            });

            _inFlightReviewCancellation[key] = cts;

            try
            {
                var command = _cliServices.CommandProvider.ReviewFileContentCommand;
                var payload = _cliServices.CommandProvider.GetReviewFileContentPayload(filename, content, _cliServices.CacheStorage.GetSolutionReviewCacheLocation());

                var (result, elapsedMs) = await ExecuteWithTimingAndLoggingAsync<CliReviewModel>(
                    "CLI file review",
                    () => _cliServices.ProcessExecutor.ExecuteAsync(command, payload, null, cts.Token),
                    $"Review of file {filename} failed");

                if (result != null)
                {
                    _ = Task.Run(async () =>
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
                        await PerformanceTelemetryHelper.SendPerformanceTelemetryAsync(GetTelemetryManager(), _logger, telemetryData);
                    });
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            finally
            {
                if (_inFlightReviewCancellation.TryGetValue(key, out var currentCts) && currentCts == cts)
                {
                    _inFlightReviewCancellation.TryRemove(key, out _);
                }

                cts.Dispose();
            }
        }

        public async Task<DeltaResponseModel> ReviewDeltaAsync(ReviewDeltaRequest request, CancellationToken cancellationToken = default)
        {
            var arguments = _cliServices.CommandProvider.GetReviewDeltaCommand(request.OldScore, request.NewScore);

            if (string.IsNullOrEmpty(arguments))
            {
                _logger.Warn("Skipping delta review. Arguments were not defined.");
                return null;
            }

            await _deltaChannel.WaitAsync(cancellationToken);
            try
            {
                var (result, elapsedMs) = await ExecuteWithTimingAndLoggingAsync<DeltaResponseModel>(
                    "CLI file delta review",
                    () => _cliServices.ProcessExecutor.ExecuteAsync(Titles.DELTA, arguments, null, cancellationToken),
                    "Delta for file failed.");

                if (result != null && !string.IsNullOrEmpty(request.FilePath))
                {
                    _ = Task.Run(async () =>
                    {
                        var loc = PerformanceTelemetryHelper.CalculateLineCount(request.FileContent);
                        var language = PerformanceTelemetryHelper.ExtractLanguage(request.FilePath);
                        var telemetryData = new PerformanceTelemetryData
                        {
                            Type = Titles.DELTA,
                            ElapsedMs = elapsedMs,
                            FilePath = request.FilePath,
                            Loc = loc,
                            Language = language,
                        };
                        await PerformanceTelemetryHelper.SendPerformanceTelemetryAsync(GetTelemetryManager(), _logger, telemetryData);
                    });
                }

                return result;
            }
            finally
            {
                _deltaChannel.Release();
            }
        }

        public async Task<PreFlightResponseModel> PreflightAsync(bool force = true)
        {
            var arguments = _cliServices.CommandProvider.GetPreflightSupportInformationCommand(force: force);
            if (string.IsNullOrEmpty(arguments))
            {
                _logger.Warn("Skipping preflight. Arguments were not defined.");
                return null;
            }

            var taskResult = await ExecuteWithTimingAndLoggingAsync<PreFlightResponseModel>(
                "ACE preflight",
                () => _cliServices.ProcessExecutor.ExecuteAsync(arguments, null, Codescene.VSExtension.Core.Consts.Constants.Timeout.TELEMETRYTIMEOUT),
                "Preflight failed.");
            return taskResult.Result;
        }

        public async Task<RefactorResponseModel> PostRefactoringAsync(FnToRefactorModel fnToRefactor, bool skipCache = false, string token = null)
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

            var (result, elapsedMs) = await ExecuteWithTimingAndLoggingAsync<RefactorResponseModel>(
                "ACE refactoring",
                () => _cliServices.ProcessExecutor.ExecuteAsync(arguments),
                "Refactoring failed.");

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

        public async Task<IList<FnToRefactorModel>> FnsToRefactorFromCodeSmellsAsync(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight)
        {
            return await ExecuteFnsToRefactorAsync(
                isValid: codeSmells != null && codeSmells.Count > 0,
                skipMessage: "Skipping refactoring functions from code smells. Code smells list was null or empty.",
                getPayload: cachePath => _cliServices.CommandProvider.GetRefactorWithCodeSmellsPayload(fileName, fileContent, cachePath, codeSmells, preflight),
                operationLabel: "ACE refactoring functions from code smells check");
        }

        public async Task<IList<FnToRefactorModel>> FnsToRefactorFromDeltaAsync(string fileName, string fileContent, DeltaResponseModel deltaResult, PreFlightResponseModel preflight)
        {
            return await ExecuteFnsToRefactorAsync(
                isValid: deltaResult != null,
                skipMessage: "Skipping refactoring functions from delta. Delta result was null.",
                getPayload: cachePath => _cliServices.CommandProvider.GetRefactorWithDeltaResultPayload(fileName, fileContent, cachePath, deltaResult, preflight),
                operationLabel: "ACE refactoring functions from delta check");
        }

        public async Task<string> GetDeviceIdAsync()
        {
            try
            {
                var result = await _cliServices.ProcessExecutor.ExecuteAsync(_cliServices.CommandProvider.DeviceIdCommand);
                return result?.Trim().TrimEnd('\r', '\n');
            }
            catch (Exception e)
            {
                _logger.Error("Could not get device ID", e);
                return string.Empty;
            }
        }

        public async Task<string> GetFileVersionAsync()
        {
            try
            {
                var result = await _cliServices.ProcessExecutor.ExecuteAsync(_cliServices.CommandProvider.VersionCommand);
                return result?.Trim().TrimEnd('\r', '\n');
            }
            catch (Exception e)
            {
                _logger.Error("Could not get CLI version", e);
                return string.Empty;
            }
        }

        private static string GetReviewCancellationKey(string filename, bool isBaseline) =>
            string.IsNullOrEmpty(filename) ? string.Empty : filename + (isBaseline ? ":baseline" : ":current");

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
            Task.Run(async () =>
            {
                try
                {
                    await PerformanceTelemetryHelper.SendPerformanceTelemetryAsync(telemetryManager, _logger, telemetryData);
                }
                catch (Exception e)
                {
                    _logger?.Debug($"Failed to send performance telemetry asynchronously: {e.Message}");
                }
            });
        }

        private async Task<IList<FnToRefactorModel>> ExecuteFnsToRefactorAsync(
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

            return await ExecuteFnsToRefactorCommandAsync(payloadContent, operationLabel, operationLabel + " failed.");
        }

        private async Task<IList<FnToRefactorModel>> ExecuteFnsToRefactorCommandAsync(string payloadContent, string operationLabel, string errorMessage)
        {
            _cliServices.CacheStorage.RemoveOldReviewCacheEntries();

            if (string.IsNullOrEmpty(payloadContent))
            {
                _logger.Warn("Skipping refactoring functions check. Payload content was not defined.");
                return null;
            }

            var command = _cliServices.CommandProvider.RefactorCommand;

            var (result, _) = await ExecuteWithTimingAndLoggingAsync<IList<FnToRefactorModel>>(
                operationLabel,
                () => _cliServices.ProcessExecutor.ExecuteAsync(command, payloadContent),
                errorMessage);
            return result;
        }

        private async Task<(T Result, long ElapsedMs)> ExecuteWithTimingAndLoggingAsync<T>(string label, Func<Task<string>> execute, string errorMessage)
        {
            long elapsedMs = 0;
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await execute();
                stopwatch.Stop();
                elapsedMs = stopwatch.ElapsedMilliseconds;

                _logger.Debug($"{Titles.CODESCENE} {label} completed in {elapsedMs} ms.");
                return (JsonConvert.DeserializeObject<T>(result), elapsedMs);
            }
            catch (DevtoolsException e)
            {
                _logger.Error(errorMessage, e);
                throw;
            }
            catch (Exception e)
            {
                _logger.Error(errorMessage, e);
                return (default, elapsedMs);
            }
        }
    }
}
