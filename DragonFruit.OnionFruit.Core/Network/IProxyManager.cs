// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.Threading.Tasks;

namespace DragonFruit.OnionFruit.Core.Network
{
    public interface IProxyManager
    {
        /// <summary>
        /// Gets the current access state of the proxy settings
        /// </summary>
        ProxyAccessState GetState();

        /// <summary>
        /// Gets the current proxy settings for the device
        /// </summary>
        ValueTask<IEnumerable<NetworkProxy>> GetProxy();

        /// <summary>
        /// Sets the proxy settings for the device.
        /// Passing an empty array causes the settings to be cleared.
        /// </summary>
        ValueTask<bool> SetProxy(params NetworkProxy[] proxies);
    }
}