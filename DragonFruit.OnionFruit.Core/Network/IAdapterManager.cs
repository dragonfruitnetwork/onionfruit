// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;

namespace DragonFruit.OnionFruit.Core.Network
{
    /// <summary>
    /// Describes a network adapter that is available on the current system
    /// </summary>
    /// <param name="Id">The system-local identifier</param>
    /// <param name="Name">Human-readable name for the adapter</param>
    /// <param name="IsVisible">Whether the adapter should be shown to the user</param>
    public record NetworkAdapterInfo(string Id, string Name, bool IsVisible);

    public interface IAdapterManager
    {
        NetworkComponentState DnsState { get; }
        NetworkComponentState ProxyState { get; }

        /// <summary>
        /// Event raised when a network adapter has been connected.
        /// </summary>
        event EventHandler<NetworkAdapterInfo> AdapterConnected;

        /// <summary>
        /// Initialises any platform-specific components and checks permissions for modifying network settings
        /// </summary>
        void Init();

        /// <summary>
        /// Retrieves a <see cref="INetworkAdapter"/> by its identifier
        /// </summary>
        /// <param name="id">The identifier to retrieve configuration for</param>
        /// <returns>A <see cref="INetworkAdapter"/> if found, otherwise <c>null</c></returns>
        /// <remarks>Instances returned by this method are not shared, so should be disposed if the underlying type inherits from <see cref="IDisposable"/></remarks>
        INetworkAdapter GetAdapter(string id);

        /// <summary>
        /// Retrieves a list of all <see cref="INetworkAdapter"/>s currently connected to the system.
        /// </summary>
        /// <returns>A read-only collection of <see cref="INetworkAdapter"/></returns>
        /// <remarks>Instances returned by this method are not shared, so should be disposed if the underlying type inherits from <see cref="IDisposable"/></remarks>
        IList<INetworkAdapter> GetAdapters();
    }
}