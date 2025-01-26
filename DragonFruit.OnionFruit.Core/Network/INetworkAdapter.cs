// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.Net;

namespace DragonFruit.OnionFruit.Core.Network
{
    public interface INetworkAdapter
    {
        /// <summary>
        /// The internal identifier for this adapter
        /// </summary>
        string Id { get; }

        /// <summary>
        /// A human-readable name for the adapter
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether the adapter should be shown to the user
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// Gets the currently configured proxy servers (and whether they are enabled)
        /// </summary>
        /// <returns></returns>
        IList<NetworkProxy> GetProxyServers();

        /// <summary>
        /// Sets the proxy servers for this adapter
        /// </summary>
        bool SetProxyServers(IReadOnlyList<NetworkProxy> servers);

        /// <summary>
        /// Gets a list of currently configured DNS servers (the user has chosen, if any)
        /// </summary>
        IList<IPAddress> GetDnsServers();

        /// <summary>
        /// Sets the DNS servers for this adapter, optionally clearing any pre-existing configuration
        /// </summary>
        bool SetDnsServers(IReadOnlyList<IPAddress> servers, bool clearExisting);
    }
}