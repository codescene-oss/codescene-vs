// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Exceptions;
using Codescene.VSExtension.Core.Interfaces;
using Codescene.VSExtension.Core.Interfaces.Cli;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Models.Cli.Telemetry;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Codescene.VSExtension.Core.Application.Cli.Rpc
{
    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1204", Justification = "Private static helpers follow the instance methods that use them.")]
    public sealed class IdeServerClient : IIdeServerClient
    {
        private readonly ILogger _logger;
        private readonly JsonRpc _rpc;
        private bool _disposed;

        public IdeServerClient(JsonRpc rpc, ILogger logger)
        {
            _rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _rpc.Disconnected += OnDisconnected;
            RegisterNotifications();
        }

        public event EventHandler<ServerStartMetadata> Started;

        public event EventHandler<FileReviewNotification> FileReviewReceived;

        public event EventHandler<DeltaReviewNotification> DeltaReviewReceived;

        public event EventHandler<ReviewFailedNotification> ReviewFailedReceived;

        public event EventHandler Disconnected;

        public bool IsConnected => !_disposed && !_rpc.IsDisposed;

        public static IdeServerClient Attach(Stream sendingStream, Stream receivingStream, ILogger logger)
        {
            if (sendingStream == null)
            {
                throw new ArgumentNullException(nameof(sendingStream));
            }

            if (receivingStream == null)
            {
                throw new ArgumentNullException(nameof(receivingStream));
            }

            var formatter = new JsonMessageFormatter(new UTF8Encoding(false))
            {
                JsonSerializer =
                {
                    NullValueHandling = NullValueHandling.Ignore,
                    DefaultValueHandling = DefaultValueHandling.Include,
                },
            };
            var handler = new HeaderDelimitedMessageHandler(sendingStream, receivingStream, formatter);
            var rpc = new JsonRpc(handler)
            {
                SynchronizationContext = null,
            };
            var client = new IdeServerClient(rpc, logger);
            rpc.StartListening();
            return client;
        }

        public Task<CliReviewModel> ReviewAsync(ReviewRequestModel request, CancellationToken cancellationToken = default)
        {
            return InvokeAsync<CliReviewModel>(RpcMethodNames.Review, request, cancellationToken);
        }

        public Task<DeltaResponseModel> DeltaAsync(DeltaRequestParams request, CancellationToken cancellationToken = default)
        {
            return InvokeAsync<DeltaResponseModel>(RpcMethodNames.Delta, request, cancellationToken);
        }

        public Task<PreFlightResponseModel> PreflightAsync(bool force = true, CancellationToken cancellationToken = default)
        {
            return InvokeAsync<PreFlightResponseModel>(
                RpcMethodNames.Preflight,
                new PreflightRequestParams { Force = force ? true : (bool?)null },
                cancellationToken);
        }

        public Task<IList<FnToRefactorModel>> FnsToRefactorAsync(FnsToRefactorRequestModel request, CancellationToken cancellationToken = default)
        {
            return InvokeAsync<IList<FnToRefactorModel>>(RpcMethodNames.FnsToRefactor, request, cancellationToken);
        }

        public Task<RefactorResponseModel> RefactorAsync(RefactorPostRequestModel request, CancellationToken cancellationToken = default)
        {
            return InvokeAsync<RefactorResponseModel>(RpcMethodNames.Refactor, request, cancellationToken);
        }

        public Task<TelemetryResponse> TelemetryAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken = default)
        {
            return InvokeAsync<TelemetryResponse>(
                RpcMethodNames.Telemetry,
                new TelemetryRequest { Event = telemetryEvent },
                cancellationToken);
        }

        public async Task<string> DeviceIdAsync(CancellationToken cancellationToken = default)
        {
            var response = await InvokeAsync<DeviceIdResponse>(RpcMethodNames.DeviceId, new EmptyRpcParams(), cancellationToken);
            return response?.DeviceId ?? string.Empty;
        }

        public void ReviewFiles(ReviewFilesParams request)
        {
            Notify(RpcMethodNames.ReviewFiles, request);
        }

        public void WatchFiles(WatchFilesParams request)
        {
            Notify(RpcMethodNames.WatchFiles, request);
        }

        public void StopWatchFiles(StopWatchFilesParams request)
        {
            Notify(RpcMethodNames.StopWatchFiles, request);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _rpc.Disconnected -= OnDisconnected;
            _rpc.Dispose();
        }

        private void RegisterNotifications()
        {
            _rpc.AddLocalRpcTarget(
                new IncomingNotifications(this),
                new JsonRpcTargetOptions
                {
                    UseSingleObjectParameterDeserialization = true,
                });
        }

        private void OnStart(JToken token)
        {
            Started?.Invoke(this, RpcJsonNormalizer.Deserialize<ServerStartMetadata>(UnwrapParams(token)));
        }

        private void OnFileReview(JToken token)
        {
            FileReviewReceived?.Invoke(this, RpcJsonNormalizer.Deserialize<FileReviewNotification>(UnwrapParams(token)));
        }

        private void OnDeltaReview(JToken token)
        {
            DeltaReviewReceived?.Invoke(this, RpcJsonNormalizer.Deserialize<DeltaReviewNotification>(UnwrapParams(token)));
        }

        private void OnReviewFailed(JToken token)
        {
            ReviewFailedReceived?.Invoke(this, RpcJsonNormalizer.Deserialize<ReviewFailedNotification>(UnwrapParams(token)));
        }

        private static JToken UnwrapParams(JToken token)
        {
            if (token is JArray array && array.Count == 1)
            {
                return array[0];
            }

            return token;
        }

        private async Task<T> InvokeAsync<T>(string method, object argument, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            try
            {
                var token = await _rpc.InvokeWithParameterObjectAsync<JToken>(method, argument, cancellationToken).ConfigureAwait(false);
                return RpcJsonNormalizer.Deserialize<T>(token);
            }
            catch (RemoteInvocationException ex)
            {
                throw new DevtoolsException(ex.Message, (int)ex.ErrorCode, null);
            }
        }

        private void Notify(string method, object argument)
        {
            ThrowIfDisposed();
            _ = NotifyAsync(method, argument);
        }

        private async Task NotifyAsync(string method, object argument)
        {
            try
            {
                await _rpc.NotifyWithParameterObjectAsync(method, argument);
            }
            catch (Exception ex)
            {
                _logger.Debug($"Failed to send {method}: {ex.Message}");
            }
        }

        private void OnDisconnected(object sender, JsonRpcDisconnectedEventArgs e)
        {
            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed || _rpc.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(IdeServerClient));
            }
        }

        private sealed class IncomingNotifications
        {
            private readonly IdeServerClient _client;

            public IncomingNotifications(IdeServerClient client)
            {
                _client = client;
            }

            [JsonRpcMethod(RpcMethodNames.Start)]
            public void Start(JToken token)
            {
                _client.OnStart(token);
            }

            [JsonRpcMethod(RpcMethodNames.FileReview)]
            public void FileReview(JToken token)
            {
                _client.OnFileReview(token);
            }

            [JsonRpcMethod(RpcMethodNames.DeltaReview)]
            public void DeltaReview(JToken token)
            {
                _client.OnDeltaReview(token);
            }

            [JsonRpcMethod(RpcMethodNames.ReviewFailed)]
            public void ReviewFailed(JToken token)
            {
                _client.OnReviewFailed(token);
            }
        }
    }
}
