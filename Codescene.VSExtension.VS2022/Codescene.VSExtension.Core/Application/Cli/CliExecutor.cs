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
using Codescene.VSExtension.Core.Application.Util;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Interfaces.Telemetry;
using Codescene.VSExtension.Core.Interfaces.Util;
using Codescene.VSExtension.Core.Models;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Util;
using static Codescene.VSExtension.Core.Consts.Constants;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(ICliExecutor))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class CliExecutor : ICliExecutor
    {
        private readonly ILogger _logger;
        private readonly IIdeServerClient _client;
        private readonly ICacheStorageService _cacheStorage;
        private readonly ISettingsProvider _settingsProvider;
        private readonly Lazy<ITelemetryManager> _telemetryManagerLazy;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _inFlightReviewCancellation = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly ConcurrentDictionary<string, Lazy<Task<IList<FnToRefactorModel>>>> _pendingRefactorRequests = new ConcurrentDictionary<string, Lazy<Task<IList<FnToRefactorModel>>>>();
        private readonly SemaphoreSlim _cliCommandChannel;
        private readonly SemaphoreSlim _deltaChannel = new SemaphoreSlim(1, 1);
        private readonly ICpuUsageThrottler _cpuUsageThrottler;

        [ImportingConstructor]
        public CliExecutor(
            ILogger logger,
            IIdeServerClient client,
            ICacheStorageService cacheStorage,
            ISettingsProvider settingsProvider,
            [Import(AllowDefault = true)] Lazy<ITelemetryManager> telemetryManagerLazy = null,
            [Import(AllowDefault = true)] ICpuUsageThrottler cpuUsageThrottler = null)
            : this(logger, client, cacheStorage, settingsProvider, telemetryManagerLazy, cpuUsageThrottler, 1)
        {
        }

        internal CliExecutor(
            ILogger logger,
            IIdeServerClient client,
            ICacheStorageService cacheStorage,
            ISettingsProvider settingsProvider,
            Lazy<ITelemetryManager> telemetryManagerLazy,
            ICpuUsageThrottler cpuUsageThrottler,
            int cliCommandConcurrencyLimit)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _cacheStorage = cacheStorage ?? throw new ArgumentNullException(nameof(cacheStorage));
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
            _telemetryManagerLazy = telemetryManagerLazy;
            _cpuUsageThrottler = cpuUsageThrottler ?? new NoOpCpuUsageThrottler();
            var effectiveLimit = Math.Max(1, cliCommandConcurrencyLimit);
            _cliCommandChannel = new SemaphoreSlim(effectiveLimit, effectiveLimit);
        }

        public async Task<CliReviewModel> ReviewContentAsync(string filePath, string content, bool isBaseline = false, CancellationToken cancellationToken = default)
        {
            var fileName = Path.GetFileName(filePath);
            var key = GetReviewCancellationKey(GetReviewCancellationPathIdentity(filePath), isBaseline);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _inFlightReviewCancellation.AddOrUpdate(key, cts, (_, existing) =>
            {
                try
                {
                    existing.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                existing.Dispose();
                return cts;
            });

            try
            {
                var request = new ReviewRequestModel
                {
                    FilePath = filePath,
                    FileContent = content,
                    CachePath = _cacheStorage.GetSolutionReviewCacheLocation(),
                };

                var (result, elapsedMs) = await ExecuteOnChannelAsync(
                    _cliCommandChannel,
                    cts.Token,
                    () => ExecuteWithTimingAndLoggingAsync(
                        "CLI file review",
                        () => _client.ReviewAsync(request, cts.Token),
                        $"Review of file {fileName} failed"));

                if (result != null)
                {
                    _ = Task.Run(async () =>
                    {
                        var loc = PerformanceTelemetryHelper.CalculateLineCount(content);
                        var language = PerformanceTelemetryHelper.ExtractLanguage(fileName);
                        var telemetryData = new PerformanceTelemetryData
                        {
                            Type = Titles.REVIEW,
                            ElapsedMs = elapsedMs,
                            FilePath = fileName,
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
            await _deltaChannel.WaitAsync(cancellationToken);
            try
            {
                await _cpuUsageThrottler.WaitForCpuAsync(cancellationToken);
                var (result, elapsedMs) = await ExecuteWithTimingAndLoggingAsync(
                    "CLI file delta review",
                    () => _client.DeltaAsync(request.OldScore, request.NewScore, cancellationToken),
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

        public async Task<PreFlightResponseModel> PreflightAsync(bool force = true, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var taskResult = await ExecuteWithTimingAndLoggingAsync(
                "ACE preflight",
                () => _client.PreflightAsync(force, cancellationToken),
                "Preflight failed.");
            return taskResult.Result;
        }

        public async Task<RefactorResponseModel> PostRefactoringAsync(FnToRefactorModel fnToRefactor, bool skipCache = false, string token = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var effectiveToken = string.IsNullOrEmpty(token) ? _settingsProvider.AuthToken : token;
            if (string.IsNullOrEmpty(effectiveToken))
            {
                var missingTokenEx = new MissingAuthTokenException("Authentication token is missing. Please set it in the extension settings.");
                if (CliExceptionWarnLogging.ShouldLogAsWarning(missingTokenEx))
                {
                    _logger.Warn(CliExceptionWarnLogging.FormatWarningMessage(missingTokenEx, "Refactoring failed."));
                }

                throw missingTokenEx;
            }

            var request = new RefactorPostRequestModel
            {
                Token = effectiveToken,
                SkipCache = skipCache ? true : (bool?)null,
            };
            if (!string.IsNullOrEmpty(fnToRefactor?.NippyB64))
            {
                request.FnToRefactorNippyB64 = fnToRefactor.NippyB64;
            }
            else
            {
                request.FnToRefactor = fnToRefactor;
            }

            var (result, elapsedMs) = await ExecuteWithTimingAndLoggingAsync(
                "ACE refactoring",
                () => _client.RefactorAsync(request, cancellationToken),
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

        public async Task<IList<FnToRefactorModel>> FnsToRefactorFromCodeSmellsAsync(string fileName, string fileContent, IList<CliCodeSmellModel> codeSmells, PreFlightResponseModel preflight, CancellationToken cancellationToken = default)
        {
            return await ExecuteFnsToRefactorAsync(
                isValid: codeSmells != null && codeSmells.Count > 0,
                skipMessage: "Skipping refactoring functions from code smells. Code smells list was null or empty.",
                createRequest: cachePath => new FnsToRefactorCodeSmellRequestModel
                {
                    FileName = fileName,
                    FileContent = fileContent,
                    CachePath = cachePath,
                    Preflight = preflight,
                    CodeSmells = codeSmells,
                },
                operationLabel: "ACE refactoring functions from code smells check",
                cancellationToken: cancellationToken);
        }

        public async Task<IList<FnToRefactorModel>> FnsToRefactorFromDeltaAsync(string fileName, string fileContent, DeltaResponseModel deltaResult, PreFlightResponseModel preflight, CancellationToken cancellationToken = default)
        {
            return await ExecuteFnsToRefactorAsync(
                isValid: deltaResult != null,
                skipMessage: "Skipping refactoring functions from delta. Delta result was null.",
                createRequest: cachePath => new FnsToRefactorDeltaRequestModel
                {
                    FileName = fileName,
                    FileContent = fileContent,
                    CachePath = cachePath,
                    Preflight = preflight,
                    DeltaResult = deltaResult,
                },
                operationLabel: "ACE refactoring functions from delta check",
                cancellationToken: cancellationToken);
        }

        public Task<string> GetDeviceIdAsync(CancellationToken cancellationToken = default)
        {
            return TryReadServerValueAsync(
                async token => await _client.DeviceIdAsync(token),
                "Could not get device ID",
                cancellationToken);
        }

        public Task<string> GetFileVersionAsync(CancellationToken cancellationToken = default)
        {
            return TryReadServerValueAsync(
                async token =>
                {
                    var metadata = await _client.StartAsync(token);
                    return metadata?.Sha;
                },
                "Could not get CLI version",
                cancellationToken);
        }

        private static string GetReviewCancellationPathIdentity(string filePath)
        {
            try
            {
                return string.IsNullOrEmpty(filePath) ? string.Empty : Path.GetFullPath(filePath);
            }
            catch
            {
                return filePath ?? string.Empty;
            }
        }

        private static string GetReviewCancellationKey(string filePathIdentity, bool isBaseline) =>
          string.IsNullOrEmpty(filePathIdentity) ? string.Empty : filePathIdentity + (isBaseline ? ":baseline" : ":current");

        private async Task<string> TryReadServerValueAsync(
            Func<CancellationToken, Task<string>> read,
            string errorMessage,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return (await read(cancellationToken))?.Trim() ?? string.Empty;
            }
            catch (Exception e)
            {
                _logger.Error(errorMessage, e);
                return string.Empty;
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

        private async Task<IList<FnToRefactorModel>> ExecuteFnsToRefactorAsync(
            bool isValid,
            string skipMessage,
            Func<string, FnsToRefactorRequestModel> createRequest,
            string operationLabel,
            CancellationToken cancellationToken = default)
        {
            if (!isValid)
            {
                _logger.Debug(skipMessage);
                return null;
            }

            var cachePath = _cacheStorage.GetSolutionReviewCacheLocation();
            var request = createRequest(cachePath);
            var pendingKey = GetPendingRefactorRequestKey(operationLabel, request);
            var lazyTask = _pendingRefactorRequests.GetOrAdd(
                pendingKey,
                __ => new Lazy<Task<IList<FnToRefactorModel>>>(() =>
                    ExecuteFnsToRefactorCommandAsync(request, operationLabel, operationLabel + " failed.", CancellationToken.None)));
            var pendingTask = lazyTask.Value;
            _ = pendingTask.ContinueWith(
                __ =>
                {
                    if (_pendingRefactorRequests.TryGetValue(pendingKey, out var currentLazyTask) && ReferenceEquals(currentLazyTask, lazyTask))
                    {
                        _pendingRefactorRequests.TryRemove(pendingKey, out _);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return await AwaitWithCancellationAsync(pendingTask, cancellationToken);
        }

        private async Task<IList<FnToRefactorModel>> ExecuteFnsToRefactorCommandAsync(FnsToRefactorRequestModel request, string operationLabel, string errorMessage, CancellationToken cancellationToken = default)
        {
            _cacheStorage.RemoveOldReviewCacheEntries();
            var (result, _) = await ExecuteWithTimingAndLoggingAsync(
                operationLabel,
                () => _client.FnsToRefactorAsync(request, cancellationToken),
                errorMessage);
            return result;
        }

        private string GetPendingRefactorRequestKey(string operationLabel, FnsToRefactorRequestModel request)
        {
            using (var sha = SHA256.Create())
            {
                var payload = (request.FileName ?? string.Empty) + "|" + (request.FileContent ?? string.Empty) + "|" + (request.CachePath ?? string.Empty);
                var bytes = Encoding.UTF8.GetBytes(payload);
                var hashBytes = sha.ComputeHash(bytes);
                var hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty).ToLowerInvariant();
                return operationLabel + "|" + hash;
            }
        }

        private async Task<T> ExecuteOnChannelAsync<T>(SemaphoreSlim channel, CancellationToken cancellationToken, Func<Task<T>> operation)
        {
            await channel.WaitAsync(cancellationToken);
            try
            {
                await _cpuUsageThrottler.WaitForCpuAsync(cancellationToken);
                return await operation();
            }
            finally
            {
                channel.Release();
            }
        }

        private async Task<T> AwaitWithCancellationAsync<T>(Task<T> task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                return await task;
            }

            var cancellationTaskSource = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(() => cancellationTaskSource.TrySetResult(true)))
            {
                var completedTask = await Task.WhenAny(task, cancellationTaskSource.Task);
                if (completedTask != task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            return await task;
        }

        private async Task<(T Result, long ElapsedMs)> ExecuteWithTimingAndLoggingAsync<T>(string label, Func<Task<T>> execute, string errorMessage)
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
