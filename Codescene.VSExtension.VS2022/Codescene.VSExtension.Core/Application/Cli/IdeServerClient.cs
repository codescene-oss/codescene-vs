// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Consts;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Interfaces.Extension;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using StreamJsonRpc;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(IIdeServerClient))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed partial class IdeServerClient : IIdeServerClient
    {
        private static readonly JsonSerializer Serializer = JsonSerializer.CreateDefault();
        private readonly ICliSettingsProvider _cliSettingsProvider;
        private readonly ILogger _logger;
        private readonly IIdeServerProcessFactory _processFactory;
        private readonly ISettingsProvider _settingsProvider;
        private readonly TimeSpan _startupTimeout;
        private readonly object _gate = new object();
        private readonly RpcNotifications _notifications;
        private Task<ServerMetadata> _startTask;
        private Task _notificationChain = Task.CompletedTask;
        private TaskCompletionSource<ServerMetadata> _pendingStart;
        private JsonRpc _rpc;
        private IIdeServerProcess _process;
        private int _startCount;
        private int _requestSeq;
        private bool _disposed;
        private bool _integrityVerified;
        private string _verifiedCliPath;

        [ImportingConstructor]
        public IdeServerClient(
            ICliSettingsProvider cliSettingsProvider,
            ILogger logger,
            [Import(AllowDefault = true)] ISettingsProvider settingsProvider = null)
            : this(cliSettingsProvider, logger, new NativeIdeServerProcessFactory(), settingsProvider, Constants.Timeout.SERVERSTARTUPTIMEOUT)
        {
        }

        internal IdeServerClient(
            ICliSettingsProvider cliSettingsProvider,
            ILogger logger,
            IIdeServerProcessFactory processFactory,
            ISettingsProvider settingsProvider = null,
            TimeSpan? startupTimeout = null)
        {
            _cliSettingsProvider = cliSettingsProvider ?? throw new ArgumentNullException(nameof(cliSettingsProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
            _settingsProvider = settingsProvider;
            _startupTimeout = startupTimeout ?? Constants.Timeout.SERVERSTARTUPTIMEOUT;
            _notifications = new RpcNotifications(this);
        }

        public event EventHandler<ReviewNotification> ReviewReceived;

        public event EventHandler<DeltaNotification> DeltaReceived;

        public event EventHandler<ReviewFailedNotification> ReviewFailed;

        public event EventHandler<WatchInventory> WatchInventoryChanged;

        public event EventHandler<ServerStartEvent> ServerStarted;

        public event EventHandler<ReviewQueue> QueueChanged;

        public event EventHandler<Exception> ServerError;

        public ServerMetadata Metadata { get; private set; }

        public async Task<ServerMetadata> StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var startTask = GetOrCreateStartTask();
            using (cancellationToken.Register(() => { }))
            {
                return await startTask.ConfigureAwait(false);
            }
        }

        public async Task RestartAsync(CancellationToken cancellationToken = default)
        {
            _logger.Info("[cs-ide] restarting");
            StopProcess(new Exception("CodeScene IDE server restarted"));
            await StartAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task<CliReviewModel> ReviewAsync(ReviewRequestModel request, CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<CliReviewModel>("cs-ide/review", request, cancellationToken);
        }

        public Task<DeltaResponseModel> DeltaAsync(string oldScore, string newScore, CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<DeltaResponseModel>(
                "cs-ide/delta",
                new DeltaRequestParams { OldScore = oldScore, NewScore = newScore },
                cancellationToken);
        }

        public Task<PreFlightResponseModel> PreflightAsync(bool force = false, CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<PreFlightResponseModel>(
                "cs-ide/preflight",
                new PreflightRequestParams { Force = force ? true : (bool?)null },
                cancellationToken);
        }

        public Task<IList<FnToRefactorModel>> FnsToRefactorAsync(FnsToRefactorRequestModel request, CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<IList<FnToRefactorModel>>("cs-ide/fns-to-refactor", request, cancellationToken);
        }

        public Task<RefactorResponseModel> RefactorAsync(RefactorPostRequestModel request, CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<RefactorResponseModel>("cs-ide/refactor", request, cancellationToken);
        }

        public Task TelemetryAsync(object eventPayload, CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<object>("cs-ide/telemetry", new TelemetryRequestParams { Event = eventPayload }, cancellationToken);
        }

        public async Task<string> DeviceIdAsync(CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync<JObject>("cs-ide/device-id", new object(), cancellationToken).ConfigureAwait(false);
            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(response ?? new JObject());
            return ((string)normalized["device-id"] ?? string.Empty).Trim();
        }

        public async Task<string> CodeHealthRulesTemplateAsync(CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync<JObject>("cs-ide/code-health-rules-template", new object(), cancellationToken).ConfigureAwait(false);
            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(response ?? new JObject());
            return (string)normalized["template"] ?? string.Empty;
        }

        public Task<CheckRulesResponse> CheckRulesAsync(string repoRoot, string path, CancellationToken cancellationToken = default)
        {
            return SendRequestAsync<CheckRulesResponse>(
                "cs-ide/check-rules",
                new CheckRulesRequestParams { RepoRoot = repoRoot, Path = path },
                cancellationToken);
        }

        public async Task<WatchInventory> GetWatchInventoryAsync(string repoRoot, CancellationToken cancellationToken = default)
        {
            var response = await SendRequestAsync<JObject>(
                "cs-ide/getWatchInventory",
                new WatchInventoryRequestParams { RepoRoot = repoRoot },
                cancellationToken).ConfigureAwait(false);
            return ParseWatchInventory(response, repoRoot);
        }

        public void ReviewFiles(string repoRoot, IReadOnlyList<ReviewFile> files)
        {
            var payload = new ReviewFilesParams
            {
                RepoRoot = repoRoot,
                Files = (files ?? Array.Empty<ReviewFile>()).Select(file => new ReviewFileParams
                {
                    Id = file.Id,
                    RelPath = file.RelPath,
                    Content = file.Content,
                }).ToList(),
            };
            var reviewFiles = payload.Files;
            _logger.Info("[cs-ide] sending reviewFiles " + LogFields.Format(
                LogFields.Field("repo", repoRoot),
                LogFields.Field("count", reviewFiles.Count),
                LogFields.Field("files", reviewFiles.Select(file => file.RelPath).ToArray()),
                LogFields.Field("withContent", reviewFiles.Count(file => file.Content != null)),
                LogFields.Field("withId", reviewFiles.Count(file => file.Id != null))));
            EnqueueNotification("cs-ide/reviewFiles", payload);
        }

        public void WatchFiles(string repoRoot, IReadOnlyList<string> relativePaths = null)
        {
            var payload = new WatchFilesParams
            {
                RepoRoot = repoRoot,
                RelativePaths = relativePaths?.ToArray(),
            };
            _logger.Info("[cs-ide] sending watchFiles " + LogFields.Format(
                LogFields.Field("repo", repoRoot),
                LogFields.Field("relativePaths", relativePaths ?? (IReadOnlyList<string>)new[] { "(whole repository)" })));
            EnqueueNotification("cs-ide/watchFiles", payload);
        }

        public void StopWatchFiles(string repoRoot, IReadOnlyList<string> relativePaths = null)
        {
            if (_rpc == null)
            {
                _logger.Info("[cs-ide] skip stopWatchFiles " + LogFields.Format(
                    LogFields.Field("repo", repoRoot),
                    LogFields.Field("reason", "not-running")));
                return;
            }

            _logger.Info("[cs-ide] sending stopWatchFiles " + LogFields.Format(
                LogFields.Field("repo", repoRoot),
                LogFields.Field("relativePaths", relativePaths ?? (IReadOnlyList<string>)new[] { "(whole repository)" })));
            var payload = new WatchFilesParams
            {
                RepoRoot = repoRoot,
                RelativePaths = relativePaths?.ToArray(),
            };
            EnqueueNotification("cs-ide/stopWatchFiles", payload);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopProcess(new Exception("CodeScene IDE server stopped"));
        }

        private Task<ServerMetadata> GetOrCreateStartTask()
        {
            lock (_gate)
            {
                if (_startTask != null)
                {
                    return _startTask;
                }

                _startTask = StartCoreAsync();
                return _startTask;
            }
        }

        private async Task<ServerMetadata> StartCoreAsync()
        {
            var cliFilePath = _cliSettingsProvider.CliFileFullPath;
            if (!File.Exists(cliFilePath))
            {
                throw new FileNotFoundException(
                    $"CodeScene CLI executable not found at {cliFilePath}. " +
                    "The CLI should be bundled with the extension. " +
                    "Please reinstall the extension or contact support if this issue persists.",
                    cliFilePath);
            }

            EnsureCliIntegrity(cliFilePath);

            var threads = ServerWorkerThreads.Resolve(_settingsProvider?.ServerWorkerThreads ?? 0);
            var arguments = "server --threads " + threads;
            var startCompletion = new TaskCompletionSource<ServerMetadata>();
            IIdeServerProcess process = null;
            JsonRpc rpc = null;

            try
            {
                process = StartLoggedProcess(cliFilePath, arguments);

                rpc = CreateRpc(process);
                rpc.AddLocalRpcTarget(_notifications);
                rpc.Disconnected += (_, e) =>
                {
                    if (e.Exception != null)
                    {
                        HandleProcessFailure(e.Exception, process);
                    }
                };

                lock (_gate)
                {
                    _process = process;
                    _rpc = rpc;
                    _pendingStart = startCompletion;
                }

                using (var timeoutCts = new CancellationTokenSource(_startupTimeout))
                using (timeoutCts.Token.Register(() =>
                    startCompletion.TrySetException(new TimeoutException($"cs-ide server did not send cs-ide/start within {_startupTimeout.TotalMilliseconds}ms"))))
                {
                    rpc.StartListening();
                    var metadata = await startCompletion.Task.ConfigureAwait(false);
                    if (!string.Equals(metadata.Sha, _cliSettingsProvider.RequiredDevToolVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"CodeScene CLI version mismatch. Expected {_cliSettingsProvider.RequiredDevToolVersion}, received {metadata.Sha}.");
                    }

                    return metadata;
                }
            }
            catch (Exception ex)
            {
                HandleProcessFailure(ex, process);
                throw;
            }
        }

        private IIdeServerProcess StartLoggedProcess(string cliFilePath, string arguments)
        {
            _logger.Info("[cs-ide] starting " + LogFields.Format(
                LogFields.Field("path", cliFilePath),
                LogFields.Field("args", arguments)));
            var process = _processFactory.Start(cliFilePath, arguments);
            _logger.Info("[cs-ide] started " + LogFields.Format(
                LogFields.Field("pid", process.Id),
                LogFields.Field("path", cliFilePath)));
            process.ErrorDataReceived += (_, e) =>
            {
                var trimmed = (e.Data ?? string.Empty).Trim();
                if (trimmed.Length > 0)
                {
                    _logger.Info("[cs-ide] " + trimmed);
                }
            };
            process.BeginErrorReadLine();
            process.Exited += (_, __) => HandleProcessFailure(new Exception("cs-ide server exited"), process);
            return process;
        }

        private JsonRpc CreateRpc(IIdeServerProcess process)
        {
            var formatter = new JsonMessageFormatter();
            formatter.JsonSerializer.ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new KebabCaseNamingStrategy(),
            };
            var handler = new HeaderDelimitedMessageHandler(process.StandardInput, process.StandardOutput, formatter);
            return new JsonRpc(handler);
        }

        private async Task<T> SendRequestAsync<T>(string method, object argument, CancellationToken cancellationToken)
        {
            var id = Interlocked.Increment(ref _requestSeq);
            var started = Stopwatch.StartNew();
            var requestSummary = IdeServerLog.SummarizeRequest(method, argument);
            _logger.Info(IdeServerLog.Sending(id, method, requestSummary));
            try
            {
                var result = await InvokeRequestAsync<T>(method, argument, cancellationToken).ConfigureAwait(false);
                _logger.Info(IdeServerLog.Received(id, method, started.ElapsedMilliseconds, IdeServerLog.SummarizeResponse(method, result)));
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(
                    "[cs-ide] request failed id=" + id + " method=" + method + " durationMs=" + started.ElapsedMilliseconds + ": " + ex.Message,
                    ex);
                throw;
            }
        }

        private async Task<T> InvokeRequestAsync<T>(string method, object argument, CancellationToken cancellationToken)
        {
            await StartAsync(cancellationToken).ConfigureAwait(false);
            JsonRpc rpc;
            lock (_gate)
            {
                rpc = _rpc;
            }

            if (rpc == null)
            {
                throw new InvalidOperationException("CodeScene IDE server is not running");
            }

            try
            {
                var raw = await rpc.InvokeWithParameterObjectAsync<JToken>(method, ToParameterObject(argument), cancellationToken).ConfigureAwait(false);
                if (raw == null || raw.Type == JTokenType.Null)
                {
                    return default;
                }

                var normalized = RpcJsonNormalizer.ToKebabCase(raw);
                return normalized.ToObject<T>(Serializer);
            }
            catch (RemoteInvocationException ex)
            {
                throw new DevtoolsException(ex.Message, ex.ErrorCode, null);
            }
        }

        private void EnqueueNotification(string method, object argument)
        {
            lock (_gate)
            {
                var prior = _notificationChain;
                _notificationChain = prior.ContinueWith(
                    _ => SendNotificationAsync(method, argument),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default).Unwrap();
            }
        }

        private async Task SendNotificationAsync(string method, object argument)
        {
            try
            {
                await StartAsync().ConfigureAwait(false);
                JsonRpc rpc;
                lock (_gate)
                {
                    rpc = _rpc;
                }

                if (rpc == null)
                {
                    throw new InvalidOperationException("CodeScene IDE server is not running");
                }

                await rpc.NotifyWithParameterObjectAsync(method, ToParameterObject(argument)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error("[cs-ide] notification failed method=" + method + ": " + ex.Message, ex);
                HandleError(ex);
            }
        }

        private object ToParameterObject(object argument)
        {
            if (argument == null)
            {
                return new Dictionary<string, object>();
            }

            var token = argument as JToken ?? JToken.FromObject(argument, Serializer);
            var normalized = RpcJsonNormalizer.ToKebabCase(token);
            var obj = normalized as JObject;
            if (obj == null)
            {
                return argument;
            }

            return obj.ToObject<Dictionary<string, object>>(Serializer);
        }

        private void EnsureCliIntegrity(string cliFilePath)
        {
            lock (_gate)
            {
                if (_integrityVerified && string.Equals(_verifiedCliPath, cliFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                CliBinaryIntegrityVerifier.Verify(cliFilePath, _cliSettingsProvider.RequiredCliBinarySha256);
                _integrityVerified = true;
                _verifiedCliPath = cliFilePath;
            }
        }

        private void HandleProcessFailure(Exception error, IIdeServerProcess process)
        {
            lock (_gate)
            {
                if (IsObsoleteProcess(process) || !HasActiveSession())
                {
                    return;
                }
            }

            StopProcess(error);
            HandleError(error);
        }

        private bool IsObsoleteProcess(IIdeServerProcess process)
        {
            if (process == null || _process == null)
            {
                return false;
            }

            return !ReferenceEquals(process, _process);
        }

        private bool HasActiveSession()
        {
            return _process != null || _rpc != null || _startTask != null;
        }

        private void StopProcess(Exception error)
        {
            IIdeServerProcess process;
            JsonRpc rpc;
            lock (_gate)
            {
                process = _process;
                rpc = _rpc;
                _process = null;
                _rpc = null;
                _startTask = null;
                var pendingStart = _pendingStart;
                _pendingStart = null;
                pendingStart?.TrySetException(error ?? new Exception("CodeScene IDE server stopped"));
            }

            try
            {
                rpc?.Dispose();
            }
            catch
            {
            }

            try
            {
                process?.Kill();
                process?.Dispose();
            }
            catch
            {
            }
        }

        private void HandleError(Exception error)
        {
            _logger.Error("cs-ide server error: " + error.Message, error);
            ServerError?.Invoke(this, error);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(IdeServerClient));
            }
        }

        private sealed class RpcNotifications
        {
            private readonly IdeServerClient _client;

            public RpcNotifications(IdeServerClient client)
            {
                _client = client;
            }

            [JsonRpcMethod("cs-ide/start", UseSingleObjectParameterDeserialization = true)]
            public void OnStart(JObject metadata) => _client.HandleStart(metadata);

            [JsonRpcMethod("cs-ide/fileReview", UseSingleObjectParameterDeserialization = true)]
            public void OnFileReview(JObject notification) => _client.HandleReview(notification);

            [JsonRpcMethod("cs-ide/deltaReview", UseSingleObjectParameterDeserialization = true)]
            public void OnDeltaReview(JObject notification) => _client.HandleDelta(notification);

            [JsonRpcMethod("cs-ide/reviewFailed", UseSingleObjectParameterDeserialization = true)]
            public void OnReviewFailed(JObject notification) => _client.HandleReviewFailure(notification);

            [JsonRpcMethod("cs-ide/reviewProgress", UseSingleObjectParameterDeserialization = true)]
            public void OnReviewProgress(JObject notification) => _client.HandleReviewProgress(notification);

            [JsonRpcMethod("cs-ide/watchInventoryChanged", UseSingleObjectParameterDeserialization = true)]
            public void OnWatchInventoryChanged(JObject notification) => _client.HandleWatchInventory(notification);
        }
    }
}
