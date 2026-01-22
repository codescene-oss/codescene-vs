using Codescene.VSExtension.Core.Interfaces.Util;
using System.ComponentModel.Composition;

namespace Codescene.VSExtension.Core.Application.Util
{
    /// <summary>
    /// Implementation of INetworkService that checks system network connectivity.
    /// </summary>
    [Export(typeof(INetworkService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class NetworkService : INetworkService
    {
        /// <inheritdoc />
        public bool IsNetworkAvailable()
        {
            return System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
        }
    }
}
