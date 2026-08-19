// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;
using static Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ICliExecutor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliExecutor : ICliExecutor
    {
        private readonly ILogger _logger;
        private readonly IIdeServerHost _host;
        private readonly ICacheStorageService _cacheStorage;
        private readonly ISettingsProvider _settingsProvider;
        private readonly Lazy<ITelemetryManager> _telemetryManagerLazy;
        private readonly ConcurrentDictionary<string, Lazy<Task<IList<FnToRefactorModel>>>> _pendingRefactorRequests = new ConcurrentDictionary<string, Lazy<Task<IList<FnToRefactorModel>>>>();

        [ImportingConstructor]
        public CliExecutor(
            ILogger logger,
            IIdeServerHost host,
            ICacheStorageService cacheStorage,
            ISettingsProvider settingsProvider,
            [Import(AllowDefault = true)] Lazy<ITelemetryManager> telemetryManagerLazy = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _cacheStorage = cacheStorage ?? throw new ArgumentNullException(nameof(cacheStorage));
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
            _telemetryManagerLazy = telemetryManagerLazy;
        }

        private IIdeServerClient Client =>
            _host.Client ?? throw new InvalidOperationException("CodeScene IDE server is not running.");

        public async Task<CliReviewModel> ReviewContentAsync(string filePath, string content, bool isBaseline = false, CancellationToken cancellationToken = default)
        {
            var fileName = Path.GetFileName(filePath);
            var request = new ReviewRequestModel
            {
                FilePath = filePath,
                FileContent = content,
                CachePath = _cacheStorage.GetSolutionReviewCacheLocation(),
            };

            var (result, elapsedMs) = await InvokeWithTimingAsync(
                "CLI file review",
                () => Client.ReviewAsync(request, cancellationToken),
                $"Review of file {fileName} failed");

            if (result != null)
            {
                SendPerformanceTelemetry(new PerformanceTelemetryData
                {
                    Type = Titles.REVIEW,
                    ElapsedMs = elapsedMs,
                    FilePath = fileName,
                    Loc = PerformanceTelemetryHelper.CalculateLineCount(content),
                    Language = PerformanceTelemetryHelper.ExtractLanguage(fileName),
                });
            }

            return result;
        }

        public async Task<DeltaResponseModel> ReviewDeltaAsync(ReviewDeltaRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                _logger.Warn("Skipping delta review. Arguments were not defined.");
                return null;
            }

            if (string.IsNullOrEmpty(request.OldScore) && string.IsNullOrEmpty(request.NewScore))
            {
                _logger.Warn("Skipping delta review. Arguments were not defined.");
                return null;
            }

            var (result, elapsedMs) = await InvokeWithTimingAsync(
                "CLI file delta review",
                () => Client.DeltaAsync(
                    new DeltaRequestParams { OldScore = request.OldScore, NewScore = request.NewScore },
                    cancellationToken),
                "Delta for file failed.");

            if (result != null && !string.IsNullOrEmpty(request.FilePath))
            {
                SendPerformanceTelemetry(new PerformanceTelemetryData
                {
                    Type = Titles.DELTA,
                    ElapsedMs = elapsedMs,
                    FilePath = request.FilePath,
                    Loc = PerformanceTelemetryHelper.CalculateLineCount(request.FileContent),
                    Language = PerformanceTelemetryHelper.ExtractLanguage(request.FilePath),
                });
            }

            return result;
        }

        public async Task<PreFlightResponseModel> PreflightAsync(bool force = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (result, _) = await InvokeWithTimingAsync(
                "ACE preflight",
                () => Client.PreflightAsync(force, cancellationToken),
                "Preflight failed.");
            return result;
        }

        public async Task<RefactorResponseModel> PostRefactoringAsync(FnToRefactorModel fnToRefactor, bool skipCache = false, string token = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var payload = new RefactorPostRequestModel
            {
                Token = RequireAuthToken(token),
                SkipCache = skipCache ? true : (bool?)null,
                FnToRefactorNippyB64 = string.IsNullOrEmpty(fnToRefactor?.NippyB64) ? null : fnToRefactor.NippyB64,
                FnToRefactor = string.IsNullOrEmpty(fnToRefactor?.NippyB64) ? fnToRefactor : null,
            };

            var (result, elapsedMs) = await InvokeWithTimingAsync(
                "ACE refactoring",
                () => Client.RefactorAsync(payload, cancellationToken),
                "Refactoring failed.");

            if (result != null && fnToRefactor != null)
            {
                SendPerformanceTelemetry(new PerformanceTelemetryData
                {
                    Type = Titles.ACE,
                    ElapsedMs = elapsedMs,
                    Loc = PerformanceTelemetryHelper.CalculateLineCount(fnToRefactor.Body),
                    Language = PerformanceTelemetryHelper.ExtractLanguage(null, fnToRefactor),
                    FnToRefactor = fnToRefactor,
                });
            }

            return result;
        }

        public Task<IList<FnToRefactorModel>> FnsToRefactorFromCodeSmellsAsync(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight, CancellationToken cancellationToken = default)
            => ExecuteFnsToRefactorAsync(CreateCodeSmellRequest(fileName, fileContent, codeSmells, preflight), "ACE refactoring functions from code smells check", cancellationToken);

        public Task<IList<FnToRefactorModel>> FnsToRefactorFromDeltaAsync(string fileName, string fileContent, DeltaResponseModel deltaResult, PreFlightResponseModel preflight, CancellationToken cancellationToken = default)
        {
            if (deltaResult == null)
            {
                _logger.Debug("Skipping refactoring functions from delta. Delta result was null.");
                return Task.FromResult<IList<FnToRefactorModel>>(null);
            }

            return ExecuteFnsToRefactorAsync(
                new FnsToRefactorDeltaRequestModel
                {
                    FileName = fileName,
                    FileContent = fileContent,
                    Preflight = preflight,
                    CachePath = _cacheStorage.GetSolutionReviewCacheLocation(),
                    DeltaResult = deltaResult,
                },
                "ACE refactoring functions from delta check",
                cancellationToken);
        }

        public async Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await Client.DeviceIdAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _logger.Error("Could not get device ID", e);
                return string.Empty;
            }
        }

        public Task<string> GetFileVersionAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_host.Metadata?.Sha ?? string.Empty);
        }

        private string RequireAuthToken(string token)
        {
            var effectiveToken = string.IsNullOrEmpty(token) ? _settingsProvider.AuthToken : token;
            if (!string.IsNullOrEmpty(effectiveToken))
            {
                return effectiveToken;
            }

            var missingTokenEx = new MissingAuthTokenException("Authentication token is missing. Please set it in the extension settings.");
            if (CliExceptionWarnLogging.ShouldLogAsWarning(missingTokenEx))
            {
                _logger.Warn(CliExceptionWarnLogging.FormatWarningMessage(missingTokenEx, "Refactoring failed."));
            }

            throw missingTokenEx;
        }

        private FnsToRefactorRequestModel CreateCodeSmellRequest(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight)
        {
            if (codeSmells == null || codeSmells.Count == 0)
            {
                _logger.Debug("Skipping refactoring functions from code smells. Code smells list was null or empty.");
                return null;
            }

            return new FnsToRefactorCodeSmellRequestModel
            {
                FileName = fileName,
                FileContent = fileContent,
                Preflight = preflight,
                CachePath = _cacheStorage.GetSolutionReviewCacheLocation(),
                CodeSmells = codeSmells,
            };
        }

        private async Task<IList<FnToRefactorModel>> ExecuteFnsToRefactorAsync(
            FnsToRefactorRequestModel request,
            string operationLabel,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return null;
            }

            var pendingKey = GetPendingRefactorRequestKey(operationLabel, request.FileName + "|" + request.FileContent);
            var lazyTask = _pendingRefactorRequests.GetOrAdd(
                pendingKey,
                __ => new Lazy<Task<IList<FnToRefactorModel>>>(() =>
                    InvokeFnsToRefactorAsync(request, operationLabel, CancellationToken.None)));
            var pendingTask = lazyTask.Value;
            _ = pendingTask.ContinueWith(
                __ =>
                {
                    if (_pendingRefactorRequests.TryGetValue(pendingKey, out var current) && ReferenceEquals(current, lazyTask))
                    {
                        _pendingRefactorRequests.TryRemove(pendingKey, out _);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            if (!cancellationToken.CanBeCanceled || pendingTask.IsCompleted)
            {
                return await pendingTask;
            }

            var cancellationTaskSource = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(() => cancellationTaskSource.TrySetResult(true)))
            {
                var completed = await Task.WhenAny(pendingTask, cancellationTaskSource.Task);
                if (completed != pendingTask)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            return await pendingTask;
        }

        private async Task<IList<FnToRefactorModel>> InvokeFnsToRefactorAsync(FnsToRefactorRequestModel request, string operationLabel, CancellationToken cancellationToken)
        {
            _cacheStorage.RemoveOldReviewCacheEntries();
            var (result, _) = await InvokeWithTimingAsync(
                operationLabel,
                () => Client.FnsToRefactorAsync(request, cancellationToken),
                operationLabel + " failed.");
            return result;
        }

        private string GetPendingRefactorRequestKey(string operationLabel, string payloadContent)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(payloadContent ?? string.Empty);
                var hashBytes = sha.ComputeHash(bytes);
                var hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
                return operationLabel + "|" + hash;
            }
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

        private async Task<(T Result, long ElapsedMs)> InvokeWithTimingAsync<T>(string label, Func<Task<T>> execute, string errorMessage)
        {
            long elapsedMs = 0;
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await execute();
                stopwatch.Stop();
                elapsedMs = stopwatch.ElapsedMilliseconds;
                _logger.Debug($"{Titles.CODESCENE} {label} completed in {elapsedMs} ms.");
                return (result, elapsedMs);
            }
            catch (DevtoolsException e)
            {
                if (CliExceptionWarnLogging.ShouldLogAsWarning(e))
                {
                    _logger.Warn(CliExceptionWarnLogging.FormatWarningMessage(e, errorMessage));
                }
                else
                {
                    _logger.Error(errorMessage, e);
                }

                throw;
            }
            catch (OperationCanceledException)
            {
                return (default, 0);
            }
            catch (Exception e)
            {
                _logger.Error(errorMessage, e);
                return (default, elapsedMs);
            }
        }
    }
}
