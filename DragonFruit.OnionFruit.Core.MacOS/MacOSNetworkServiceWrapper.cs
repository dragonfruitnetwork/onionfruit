// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Net;
using DragonFruit.OnionFruit.Core.Network;

namespace DragonFruit.OnionFruit.Core.MacOS
{
    public class MacOSNetworkServiceWrapper(MacOSNetworkServiceInfo serviceInfo, Func<OnionFruitDaemonConnection> managerFactory) : INetworkAdapter
    {
        public string Id => serviceInfo.ServiceId;
        public string Name => serviceInfo.ServiceName;

        public bool IsVisible => true;

        public IList<NetworkProxy> GetProxyServers()
        {
            return [..managerFactory().GetProxyServers(Id)];
        }

        public bool SetProxyServers(IReadOnlyList<NetworkProxy> servers)
        {
            managerFactory().SetProxyServers(Id, servers, true);
            return true;
        }

        public IList<IPAddress> GetDnsServers()
        {
            return managerFactory().GetDnsResolvers(Id);
        }

        public bool SetDnsServers(IList<IPAddress> servers, bool clearExisting)
        {
            managerFactory().SetDnsResolvers(Id, servers);
            return true;
        }
    }
}