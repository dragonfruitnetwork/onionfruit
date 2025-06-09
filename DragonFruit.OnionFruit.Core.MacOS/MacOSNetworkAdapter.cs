// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System.Collections.Generic;
using System.Net;
using DragonFruit.OnionFruit.Core.Network;

namespace DragonFruit.OnionFruit.Core.MacOS
{
    public class MacOSNetworkAdapter(MacOSNetworkService serviceInfo, OnionFruitDaemonConnection manager) : INetworkAdapter
    {
        public string Id => serviceInfo.ServiceId;
        public string Name => serviceInfo.ServiceName;

        public bool IsVisible => true;

        public IList<NetworkProxy> GetProxyServers()
        {
            return [..manager.GetProxyServers(Id)];
        }

        public bool SetProxyServers(IReadOnlyList<NetworkProxy> servers)
        {
            manager.SetProxyServers(Id, servers);
            return true;
        }

        public IList<IPAddress> GetDnsServers()
        {
            return manager.GetDnsResolvers(Id);
        }

        public bool SetDnsServers(IList<IPAddress> servers, bool clearExisting)
        {
            manager.SetDnsResolvers(Id, servers);
            return true;
        }
    }
}