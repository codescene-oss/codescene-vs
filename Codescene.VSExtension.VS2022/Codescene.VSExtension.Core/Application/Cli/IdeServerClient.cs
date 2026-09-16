// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using StreamJsonRpc;

namespace Codescene.VSExtension.Core.Application.Cli
{
    [Export(typeof(IIdeServerClient))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public sealed class IdeServerClient : IIdeServerClient
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
        private TaskCompletionSource<ServerMetadata> _pendingStart;
        private JsonRpc _rpc;
        private IIdeServerProcess _process;
        private int _startCount;
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
            _logger.Info($"[cs-ide] sending reviewFiles count={payload.Files.Count}");
            _ = SendNotificationAsync("cs-ide/reviewFiles", payload);
        }

        public void WatchFiles(string repoRoot, IReadOnlyList<string> relativePaths = null)
        {
            var payload = new WatchFilesParams
            {
                RepoRoot = repoRoot,
                RelativePaths = relativePaths?.ToArray(),
            };
            _logger.Debug($"[cs-ide] sending watchFiles repo-root={repoRoot} relative-paths={(relativePaths == null ? "(whole repository)" : string.Join(", ", relativePaths))}");
            _ = SendNotificationAsync("cs-ide/watchFiles", payload);
        }

        public void StopWatchFiles(string repoRoot, IReadOnlyList<string> relativePaths = null)
        {
            if (_rpc == null)
            {
                return;
            }

            var payload = new WatchFilesParams
            {
                RepoRoot = repoRoot,
                RelativePaths = relativePaths?.ToArray(),
            };
            _ = SendNotificationAsync("cs-ide/stopWatchFiles", payload);
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
                process = _processFactory.Start(cliFilePath, arguments);
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

        private void HandleStart(JObject metadata)
        {
            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(metadata ?? new JObject());
            var parsed = new ServerMetadata
            {
                Sha = (string)normalized["sha"],
                Version = (string)normalized["version"],
            };
            Metadata = parsed;
            TaskCompletionSource<ServerMetadata> pendingStart;
            lock (_gate)
            {
                pendingStart = _pendingStart;
                _pendingStart = null;
            }

            pendingStart?.TrySetResult(parsed);
            var restart = Interlocked.Increment(ref _startCount) > 1;
            ServerStarted?.Invoke(this, new ServerStartEvent { Metadata = parsed, Restart = restart });
        }

        private void HandleReview(JObject notification)
        {
            var identity = ParseIdentity(notification);
            if (identity == null)
            {
                return;
            }

            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(notification);
            var queue = ParseQueue(normalized, identity.Item3);
            EmitQueue(queue);
            _logger.Info($"[cs-ide] received fileReview id={identity.Item1 ?? "(none)"} path={identity.Item2}");
            ReviewReceived?.Invoke(this, new ReviewNotification
            {
                Id = identity.Item1,
                Path = identity.Item2,
                RepoRoot = identity.Item3,
                Result = normalized["result"]?.ToObject<CliReviewModel>(Serializer),
                Queue = queue,
            });
        }

        private void HandleDelta(JObject notification)
        {
            var identity = ParseIdentity(notification);
            if (identity == null)
            {
                return;
            }

            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(notification);
            var queue = ParseQueue(normalized, identity.Item3);
            EmitQueue(queue);
            var resultToken = normalized["result"];
            _logger.Info($"[cs-ide] received deltaReview id={identity.Item1 ?? "(none)"} path={identity.Item2}");
            DeltaReceived?.Invoke(this, new DeltaNotification
            {
                Id = identity.Item1,
                Path = identity.Item2,
                RepoRoot = identity.Item3,
                Result = resultToken == null || resultToken.Type == JTokenType.Null
                    ? null
                    : resultToken.ToObject<DeltaResponseModel>(Serializer),
                Queue = queue,
            });
        }

        private void HandleReviewFailure(JObject notification)
        {
            var identity = ParseIdentity(notification);
            if (identity == null)
            {
                return;
            }

            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(notification);
            var queue = ParseQueue(normalized, identity.Item3);
            EmitQueue(queue);
            ReviewFailed?.Invoke(this, new ReviewFailedNotification
            {
                Id = identity.Item1,
                Path = identity.Item2,
                RepoRoot = identity.Item3,
                Message = (string)normalized["message"],
                Queue = queue,
            });
        }

        private void HandleWatchInventory(JObject notification)
        {
            var inventory = ParseWatchInventory(notification, null);
            if (string.IsNullOrEmpty(inventory.RepoRoot))
            {
                return;
            }

            _logger.Info($"[cs-ide] received watchInventoryChanged repo={inventory.RepoRoot} count={inventory.Files.Count}");
            WatchInventoryChanged?.Invoke(this, inventory);
        }

        private Tuple<string, string, string> ParseIdentity(JObject notification)
        {
            if (notification == null)
            {
                return null;
            }

            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(notification);
            var path = (string)normalized["path"];
            var repoRoot = (string)normalized["repo-root"];
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(repoRoot))
            {
                return null;
            }

            var idToken = normalized["id"];
            string id = null;
            if (idToken != null && idToken.Type != JTokenType.Null)
            {
                id = (string)idToken;
            }

            return Tuple.Create(id, path, repoRoot);
        }

        private WatchInventory ParseWatchInventory(JObject notification, string fallbackRepoRoot)
        {
            var normalized = (JObject)RpcJsonNormalizer.ToKebabCase(notification ?? new JObject());
            var files = normalized["files"] as JArray;
            return new WatchInventory
            {
                RepoRoot = (string)normalized["repo-root"] ?? fallbackRepoRoot,
                Files = files == null ? new List<string>() : files.Select(token => (string)token).Where(value => value != null).ToList(),
            };
        }

        private ReviewQueue ParseQueue(JObject notification, string repoRoot)
        {
            var queueToken = notification?["queue"] as JObject;
            if (queueToken == null)
            {
                return null;
            }

            var files = queueToken["files"] as JArray;
            return new ReviewQueue
            {
                Count = queueToken["count"]?.Value<int>() ?? 0,
                Files = (files ?? new JArray())
                    .Select(token => (string)token)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .Select(relPath => Path.Combine(repoRoot ?? string.Empty, relPath.Replace('/', Path.DirectorySeparatorChar)))
                    .ToList(),
            };
        }

        private void EmitQueue(ReviewQueue queue)
        {
            if (queue == null)
            {
                return;
            }

            QueueChanged?.Invoke(this, queue);
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

            [JsonRpcMethod("cs-ide/watchInventoryChanged", UseSingleObjectParameterDeserialization = true)]
            public void OnWatchInventoryChanged(JObject notification) => _client.HandleWatchInventory(notification);
        }
    }
}
