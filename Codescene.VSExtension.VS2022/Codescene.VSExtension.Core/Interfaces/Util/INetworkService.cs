// Copyright (c) CodeScene. All rights reserved.

namespace Codescene.VSExtension.Core.Interfaces.Util
{
    /// <summary>
    /// Service for checking network connectivity.
    /// </summary>
    public interface INetworkService
    {
        /// <summary>
        /// Returns true if network connectivity is available.
        /// </summary>
        bool IsNetworkAvailable();
    }
}
