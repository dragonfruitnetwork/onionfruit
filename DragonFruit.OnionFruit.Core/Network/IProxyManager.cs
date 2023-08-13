namespace DragonFruit.OnionFruit.Core.Network
{
    public interface IProxyManager
    {
        /// <summary>
        /// Gets the current proxy settings for the interface
        /// </summary>
        NetworkProxy GetProxy();

        /// <summary>
        /// Clears the proxy settings for the interface
        /// </summary>
        void ClearProxy();
        
        /// <summary>
        /// Sets the proxy settings for the interface
        /// </summary>
        /// <param name="proxy">The proxy to use</param>
        void SetProxy(NetworkProxy proxy);
    }
}