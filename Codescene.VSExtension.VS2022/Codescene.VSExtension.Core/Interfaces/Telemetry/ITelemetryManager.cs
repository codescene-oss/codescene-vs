// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Telemetry
{
    public interface ITelemetryManager
    {
        Task SendTelemetryAsync(string eventName, Dictionary<string, object> additionalEventData = null, CancellationToken cancellationToken = default);

        int HoldRouting();

        void RoutingReady(int generation);

        Task EndSessionAsync(TimeSpan timeout);
    }
}
