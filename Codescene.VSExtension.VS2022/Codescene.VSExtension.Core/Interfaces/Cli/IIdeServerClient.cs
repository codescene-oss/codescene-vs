// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Codescene.VSExtension.Core.Models.Cli.Delta;
using Codescene.VSExtension.Core.Models.Cli.Refactor;
using Codescene.VSExtension.Core.Models.Cli.Review;
using Codescene.VSExtension.Core.Models.Cli.Rpc;
using Codescene.VSExtension.Core.Models.Cli.Telemetry;

namespace Codescene.VSExtension.Core.Interfaces.Cli
{
    public interface IIdeServerClient : IDisposable
    {
        event EventHandler<ServerStartMetadata> Started;

        event EventHandler<FileReviewNotification> FileReviewReceived;

        event EventHandler<DeltaReviewNotification> DeltaReviewReceived;

        event EventHandler<ReviewFailedNotification> ReviewFailedReceived;

        event EventHandler Disconnected;

        bool IsConnected { get; }

        Task<CliReviewModel> ReviewAsync(ReviewRequestModel request, CancellationToken cancellationToken = default);

        Task<DeltaResponseModel> DeltaAsync(DeltaRequestParams request, CancellationToken cancellationToken = default);

        Task<PreFlightResponseModel> PreflightAsync(bool force = true, CancellationToken cancellationToken = default);

        Task<IList<FnToRefactorModel>> FnsToRefactorAsync(FnsToRefactorRequestModel request, CancellationToken cancellationToken = default);

        Task<RefactorResponseModel> RefactorAsync(RefactorPostRequestModel request, CancellationToken cancellationToken = default);

        Task<TelemetryResponse> TelemetryAsync(TelemetryEvent telemetryEvent, CancellationToken cancellationToken = default);

        Task<string> DeviceIdAsync(CancellationToken cancellationToken = default);

        void ReviewFiles(ReviewFilesParams request);

        void WatchFiles(WatchFilesParams request);

        void StopWatchFiles(StopWatchFilesParams request);
    }
}
