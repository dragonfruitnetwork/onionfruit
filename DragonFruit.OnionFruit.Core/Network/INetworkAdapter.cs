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
        /// Gets a list of currently configured DNS servers (the user has chosen, if any)
        /// </summary>
        /// <returns></returns>
        IReadOnlyCollection<IPAddress> GetDnsServers();

        /// <summary>
        /// Sets the DNS servers for this adapter, optionally clearing any pre-existing configuration
        /// </summary>
        void SetDnsServers(IReadOnlyCollection<IPAddress> servers, bool clearExisting);
    }
}