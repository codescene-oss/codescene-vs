// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Telemetry
{
    public interface ITelemetryManager
    {
        Task SendTelemetryAsync(string eventName, Dictionary<string, object> additionalEventData = null);

        Task SendErrorTelemetryAsync(Exception ex, string context, Dictionary<string, object> extraData = null);
    }
}
