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
        private readonly object _lock = new();
        private readonly string _xpcServiceName;
        private readonly AppService _appService;

        private OnionFruitDaemonConnection _activeServiceConnection;

        public MacOSNetworkServiceManager(string xpcServiceName, string daemonPlistName)
        {
            _xpcServiceName = xpcServiceName;

            if (!string.IsNullOrEmpty(daemonPlistName))
            {
                _appService = AppService.DaemonServiceWithPlistName(daemonPlistName);
            }
        }

        public NetworkComponentState DnsState => NetworkComponentState.Unavailable;
        public NetworkComponentState ProxyState => _appService != null && _appService.Status != AppServiceStatus.Enabled ? NetworkComponentState.MissingPermissions : NetworkComponentState.Available;

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
            var serviceInfo = MacOSNetworkServiceInfo.GetNetworkServices().FirstOrDefault(x => x.ServiceId == id);
            if (serviceInfo == null)
            {
                throw new ArgumentException($"No network service found with ID '{id}'", nameof(id));
            }

            return new MacOSNetworkServiceWrapper(serviceInfo, GetDaemonConnection);
        }

        public IList<INetworkAdapter> GetAdapters()
        {
            var serviceInfo = MacOSNetworkServiceInfo.GetNetworkServices();
            var interfaces = NetworkInterface.GetAllNetworkInterfaces().Where(x => x.OperationalStatus == OperationalStatus.Up);

            return [..serviceInfo.Join(interfaces, x => x.BsdInterfaceId, x => x.Id, (service, _) => new MacOSNetworkServiceWrapper(service, GetDaemonConnection))];
        }

        private OnionFruitDaemonConnection GetDaemonConnection()
        {
            lock (_lock)
            {
                if (_activeServiceConnection?.IsValid != true)
                {
                    _activeServiceConnection?.Dispose();
                    _activeServiceConnection = new OnionFruitDaemonConnection(_xpcServiceName);
                }
            }

            return _activeServiceConnection;
        }

        public void Dispose()
        {
            _activeServiceConnection?.Dispose();
            _appService?.Dispose();
        }
    }
}