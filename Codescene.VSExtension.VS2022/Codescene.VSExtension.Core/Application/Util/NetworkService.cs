// Copyright (c) CodeScene. All rights reserved.

using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using Codescene.VSExtension.Core.Interfaces.Util;

namespace Codescene.VSExtension.Core.Application.Util
{
    /// <summary>
    /// Implementation of INetworkService that checks system network connectivity.
    /// </summary>
    [Export(typeof(INetworkService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    [ExcludeFromCodeCoverage]
    public class NetworkService : INetworkService
    {
        /// <inheritdoc />
        public bool IsNetworkAvailable()
        {
            return System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
        }
    }
}
