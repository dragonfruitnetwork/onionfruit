// OnionFruit Copyright DragonFruit Network <inbox@dragonfruit.network>
// Licensed under LGPL-3.0. Refer to the LICENCE file for more info

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security;
using System.Security.AccessControl;
using DragonFruit.OnionFruit.Core.Network;
using Microsoft.Win32;
using WmiLight;

namespace DragonFruit.OnionFruit.Core.Windows
{
    public class WindowsNetworkAdapterManager : IDisposable, INetworkAdapterManager
    {
        private const string WmiNamespace = @"root\cimv2";
        private const string WmiNetworkAdapterQuery = "SELECT Description,SettingID FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = 'TRUE'";
        private const string WmiNetworkAdapterEventQuery = "SELECT * FROM __InstanceCreationEvent WITHIN 10 WHERE TargetInstance ISA 'Win32_NetworkAdapterConfiguration' AND TargetInstance.IPEnabled = TRUE";

        private const string DnsRegistryPath = @"SYSTEM\CurrentControlSet\Services\Tcpip{0}\Parameters\Interfaces";
        private const string ProxySettingsPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        private const RegistryRights RequiredRegistryAccess = RegistryRights.ReadPermissions
                                                              | RegistryRights.ReadKey
                                                              | RegistryRights.WriteKey
                                                              | RegistryRights.SetValue
                                                              | RegistryRights.Delete;

        private WmiConnection _wmiEventConnection;
        private WmiEventSubscription _wmiEventSubscription;
        private RegistryKey _proxyRegistry, _tcpipConfigRegistry, _tcpip6ConfigRegistry;

        public NetworkComponentState DnsState => _tcpipConfigRegistry == null && _tcpip6ConfigRegistry == null ? NetworkComponentState.MissingPermissions : NetworkComponentState.Available;
        public NetworkComponentState ProxyState => _proxyRegistry == null ? NetworkComponentState.MissingPermissions : NetworkComponentState.Available;

        public event EventHandler<NetworkAdapterInfo> AdapterConnected;

        public void Init()
        {
            try
            {
                _proxyRegistry?.Dispose();
                _proxyRegistry = Registry.CurrentUser.OpenSubKey(ProxySettingsPath, RegistryKeyPermissionCheck.ReadWriteSubTree, RequiredRegistryAccess);
            }
            catch (SecurityException)
            {
            }

            try
            {
                _tcpipConfigRegistry?.Dispose();
                _tcpip6ConfigRegistry?.Dispose();

                _tcpipConfigRegistry = Registry.LocalMachine.OpenSubKey(string.Format(DnsRegistryPath, string.Empty), RegistryKeyPermissionCheck.ReadWriteSubTree, RequiredRegistryAccess);
                _tcpip6ConfigRegistry = Registry.LocalMachine.OpenSubKey(string.Format(DnsRegistryPath, "6"), RegistryKeyPermissionCheck.ReadWriteSubTree, RequiredRegistryAccess);
            }
            catch (SecurityException)
            {
            }

            _wmiEventConnection?.Dispose();
            _wmiEventConnection = new WmiConnection(WmiNamespace);

            _wmiEventSubscription?.Dispose();
            _wmiEventSubscription = _wmiEventConnection.CreateEventSubscription(WmiNetworkAdapterEventQuery, WmiEventHandler);
        }

        public INetworkAdapter GetAdapter(string id)
        {
            var targetAdapter = _wmiEventConnection.CreateQuery($"{WmiNetworkAdapterQuery} AND SettingID = '{id}'").SingleOrDefault();
            return targetAdapter == null ? null : AdapterFromWmiInstance(targetAdapter);
        }

        public IList<INetworkAdapter> GetAdapters()
        {
            // WMI uses COM, so each connection needs to be tied to the thread it's running on.
            // creating the connection here to keep it simple and thread-safe.
            using var wmiConnection = new WmiConnection(WmiNamespace);

            return
            [
                new WinGlobalProxyAdapter(_proxyRegistry, false),
                ..wmiConnection.CreateQuery(WmiNetworkAdapterQuery).Select(AdapterFromWmiInstance)
            ];
        }

        private void WmiEventHandler(WmiObject x)
        {
            var newEvent = x.GetPropertyValue<WmiObject>("TargetInstance");
            if (newEvent == null)
            {
                return;
            }

            var id = x.GetPropertyValue<string>("SettingID");
            var name = x.GetPropertyValue<string>("Description");

            AdapterConnected?.Invoke(this, new NetworkAdapterInfo(id, name, true));
        }

        public void Dispose()
        {
            _wmiEventSubscription?.Dispose();
            _wmiEventConnection?.Dispose();

            _proxyRegistry?.Dispose();
            _tcpipConfigRegistry?.Dispose();
            _tcpip6ConfigRegistry?.Dispose();

            GC.SuppressFinalize(this);
        }

        private WinNetworkAdapter AdapterFromWmiInstance(WmiObject instance)
        {
            var id = instance.GetPropertyValue<string>("SettingID");
            var name = instance.GetPropertyValue<string>("Description");

            var tcpipKey = Socket.OSSupportsIPv4 ? _tcpipConfigRegistry?.OpenSubKey(id, RequiredRegistryAccess) : null;
            var tcpip6Key = Socket.OSSupportsIPv6 ? _tcpip6ConfigRegistry?.OpenSubKey(id, RequiredRegistryAccess) : null;

            return new WinNetworkAdapter(id, name, !string.IsNullOrEmpty(name), tcpipKey, tcpip6Key);
        }
    }
}