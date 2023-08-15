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
        /// Gets the current proxy settings for the interface
        /// </summary>
        NetworkProxy GetProxy();

        /// <summary>
        /// Clears the proxy settings for the interface
        /// </summary>
        ValueTask ClearProxy();
        
        /// <summary>
        /// Sets the proxy settings for the interface
        /// </summary>
        /// <param name="proxy">The proxy to use</param>
        ValueTask SetProxy(NetworkProxy proxy);
    }
}