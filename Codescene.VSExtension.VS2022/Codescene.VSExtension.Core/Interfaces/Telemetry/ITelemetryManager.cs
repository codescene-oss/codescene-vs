// Copyright (c) CodeScene. All rights reserved.

using System;
using System.Collections.Generic;

namespace Codescene.VSExtension.Core.Interfaces.Telemetry
{
    public interface ITelemetryManager
    {
        void SendTelemetry(string eventName, Dictionary<string, object> additionalEventData = null);

        void SendErrorTelemetry(Exception ex, string context, Dictionary<string, object> extraData = null);
    }
}
