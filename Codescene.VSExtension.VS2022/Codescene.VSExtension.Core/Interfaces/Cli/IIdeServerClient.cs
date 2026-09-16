// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IIdeServerClient : IDisposable
    {
        event EventHandler<ReviewNotification> ReviewReceived;

        event EventHandler<DeltaNotification> DeltaReceived;

        event EventHandler<ReviewFailedNotification> ReviewFailed;

        event EventHandler<WatchInventory> WatchInventoryChanged;

        event EventHandler<ServerStartEvent> ServerStarted;

        event EventHandler<ReviewQueue> QueueChanged;

        event EventHandler<Exception> ServerError;

        ServerMetadata Metadata { get; }

        Task<ServerMetadata> StartAsync(CancellationToken cancellationToken = default);

        Task RestartAsync(CancellationToken cancellationToken = default);

        Task<CliReviewModel> ReviewAsync(ReviewRequestModel request, CancellationToken cancellationToken = default);

        Task<DeltaResponseModel> DeltaAsync(string oldScore, string newScore, CancellationToken cancellationToken = default);

        Task<PreFlightResponseModel> PreflightAsync(bool force = false, CancellationToken cancellationToken = default);

        Task<IList<FnToRefactorModel>> FnsToRefactorAsync(FnsToRefactorRequestModel request, CancellationToken cancellationToken = default);

        Task<RefactorResponseModel> RefactorAsync(RefactorPostRequestModel request, CancellationToken cancellationToken = default);

        Task TelemetryAsync(object eventPayload, CancellationToken cancellationToken = default);

        Task<string> DeviceIdAsync(CancellationToken cancellationToken = default);

        Task<string> CodeHealthRulesTemplateAsync(CancellationToken cancellationToken = default);

        Task<CheckRulesResponse> CheckRulesAsync(string repoRoot, string path, CancellationToken cancellationToken = default);

        Task<WatchInventory> GetWatchInventoryAsync(string repoRoot, CancellationToken cancellationToken = default);

        void ReviewFiles(string repoRoot, IReadOnlyList<ReviewFile> files);

        void WatchFiles(string repoRoot, IReadOnlyList<string> relativePaths = null);

        void StopWatchFiles(string repoRoot, IReadOnlyList<string> relativePaths = null);
    }
}
