// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using AppServiceSharp;
using AppServiceSharp.Enums;
using DragonFruit.OnionFruit.Core.Network;

namespace DragonFruit.OnionFruit.Core.MacOS
{
    public class MacOSNetworkServiceManager : INetworkAdapterManager, IDisposable
    {
        private readonly OnionFruitDaemonConnection _serviceConnection;
        private readonly AppService _appService;

        public MacOSNetworkServiceManager(string xpcServiceName, string daemonPlistName)
        {
            _serviceConnection = new OnionFruitDaemonConnection(xpcServiceName);

            if (!string.IsNullOrEmpty(daemonPlistName))
            {
                _appService = AppService.DaemonServiceWithPlistName(daemonPlistName);
            }
        }

        public NetworkComponentState DnsState => NetworkComponentState.Unavailable;
        public NetworkComponentState ProxyState => _appService?.Status != AppServiceStatus.Enabled ? NetworkComponentState.MissingPermissions : NetworkComponentState.Available;

        public event EventHandler<NetworkAdapterInfo> AdapterConnected;

        public void Init()
        {
            if (_appService != null && _appService.Status != AppServiceStatus.Enabled)
            {
                _appService.RegisterService();
            }
        }

        public INetworkAdapter GetAdapter(string id)
        {
            var serviceInfo = MacOSNetworkService.GetNetworkServices().FirstOrDefault(x => x.ServiceId == id);
            if (serviceInfo == null)
            {
                throw new ArgumentException($"No network service found with ID '{id}'", nameof(id));
            }

            return new MacOSNetworkAdapter(serviceInfo, _serviceConnection);
        }

        public IList<INetworkAdapter> GetAdapters()
        {
            var serviceInfo = MacOSNetworkService.GetNetworkServices();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up);

            return [..serviceInfo.Join(interfaces, x => x.BsdInterfaceId, x => x.Id, (service, _) => new MacOSNetworkAdapter(service, _serviceConnection))];
        }

        public void Dispose()
        {
            _serviceConnection?.Dispose();
            _appService?.Dispose();
        }
    }
}