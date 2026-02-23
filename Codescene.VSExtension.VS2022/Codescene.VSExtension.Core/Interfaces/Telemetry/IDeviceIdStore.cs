// Copyright (c) CodeScene. All rights reserved.

using System.Threading.Tasks;

namespace Codescene.VSExtension.Core.Interfaces.Telemetry
{
    /// <summary>
    /// Provides access to a unique device identifier.
    /// </summary>
    /// <remarks>
    /// The device ID is used to help identify individual users when sending telemetry events.
    /// It should remain consistent across extension sessions on the same machine.
    /// </remarks>
    public interface IDeviceIdStore
    {
        Task<string> GetDeviceIdAsync();
    }
}
